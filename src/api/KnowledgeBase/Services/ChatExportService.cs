using System.Text.Json;
using System.Text.RegularExpressions;
using Intranet.Api.KnowledgeBase.Data;
using Intranet.Api.KnowledgeBase.Data.Entities;
using Intranet.Api.KnowledgeBase.Models;
using Intranet.Api.KnowledgeBase.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intranet.Api.KnowledgeBase.Services;

public sealed class ChatExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly OllamaClient _ollama;
    private readonly KnowledgeDbContext _db;
    private readonly KnowledgeBaseOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ChatExportService> _logger;

    public ChatExportService(
        OllamaClient ollama,
        KnowledgeDbContext db,
        IOptions<KnowledgeBaseOptions> options,
        IWebHostEnvironment env,
        ILogger<ChatExportService> logger)
    {
        _ollama = ollama;
        _db = db;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<BuiltExport> BuildExportAsync(
        ExportFormat format,
        string query,
        string context,
        string baseSystemPrompt,
        CancellationToken cancellationToken) =>
        await BuildAsync(format, query, context, baseSystemPrompt, cancellationToken);

    public async Task<ChatAttachmentDto> SaveExportAsync(
        BuiltExport built,
        Guid sessionId,
        Guid messageId,
        string? userOid,
        Guid? projectId,
        CancellationToken cancellationToken) =>
        await SaveAsync(built, sessionId, messageId, userOid, projectId, cancellationToken);

    public async Task<(string Answer, ChatAttachmentDto Attachment)> GenerateAsync(
        ExportFormat format,
        string query,
        string context,
        string baseSystemPrompt,
        Guid sessionId,
        Guid messageId,
        string? userOid,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var built = await BuildAsync(format, query, context, baseSystemPrompt, cancellationToken);
        var attachment = await SaveAsync(
            built,
            sessionId,
            messageId,
            userOid,
            projectId,
            cancellationToken);
        return (built.Answer, attachment);
    }

    private async Task<BuiltExport> BuildAsync(
        ExportFormat format,
        string query,
        string context,
        string baseSystemPrompt,
        CancellationToken cancellationToken)
    {
        var formatLabel = ChatExportIntent.FormatLabel(format);
        var jsonPrompt = BuildJsonPrompt(format, query, context);
        var systemPrompt = $"""
            {baseSystemPrompt}

            The user asked for a {formatLabel} file. Respond with ONLY valid JSON matching the schema below.
            Do not wrap JSON in markdown fences. Use facts from the provided context only.
            If data is incomplete, include what you have and note gaps in a summary row or section.
            For Excel: row values may be strings or numbers. Use Excel formulas (e.g. "=B2*35") in cells when totals depend on other columns.
            """;

        var raw = await _ollama.ChatAsync(systemPrompt, jsonPrompt, cancellationToken);
        var json = ExtractJson(raw);

        byte[] bytes;
        string filename;
        string mimeType;
        string formatKey;

        if (format == ExportFormat.Excel)
        {
            var spec = JsonSerializer.Deserialize<ExcelExportSpec>(json, JsonOptions)
                ?? throw new InvalidOperationException("Could not parse Excel export JSON from the model.");
            bytes = ChatExcelBuilder.Build(spec);
            filename = SanitizeFilename(spec.Filename, "export.xlsx", ".xlsx");
            mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            formatKey = "xlsx";
        }
        else
        {
            var spec = JsonSerializer.Deserialize<WordExportSpec>(json, JsonOptions)
                ?? throw new InvalidOperationException("Could not parse Word export JSON from the model.");
            bytes = ChatWordBuilder.Build(spec);
            filename = SanitizeFilename(spec.Filename, "export.docx", ".docx");
            mimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            formatKey = "docx";
        }

        var answer =
            $"I created {filename} ({formatLabel}). Use the download button below. " +
            "Content is based on the sources used for this answer.";

        return new BuiltExport(answer, bytes, filename, mimeType, formatKey);
    }

    private async Task<ChatAttachmentDto> SaveAsync(
        BuiltExport built,
        Guid sessionId,
        Guid messageId,
        string? userOid,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var fileId = Guid.NewGuid();
        var storagePath = SaveBytes(fileId, built.Bytes, built.Filename);

        _db.GeneratedFiles.Add(new KbGeneratedFile
        {
            Id = fileId,
            SessionId = sessionId,
            MessageId = messageId,
            UserOid = userOid,
            ProjectId = projectId,
            Filename = built.Filename,
            MimeType = built.MimeType,
            Format = built.FormatKey,
            StoragePath = storagePath,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Generated {Format} file {FileId} ({Filename}, {Bytes} bytes) for session {SessionId}",
            built.FormatKey,
            fileId,
            built.Filename,
            built.Bytes.Length,
            sessionId);

        return new ChatAttachmentDto(fileId, built.Filename, built.MimeType, built.FormatKey);
    }

    public sealed record BuiltExport(
        string Answer,
        byte[] Bytes,
        string Filename,
        string MimeType,
        string FormatKey);

    public async Task<(KbGeneratedFile File, byte[] Bytes)?> TryReadForUserAsync(
        Guid fileId,
        string? userOid,
        CancellationToken cancellationToken)
    {
        var file = await _db.GeneratedFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId, cancellationToken);

        if (file is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(userOid) && !string.IsNullOrEmpty(file.UserOid)
            && !string.Equals(file.UserOid, userOid, StringComparison.Ordinal))
        {
            return null;
        }

        if (!File.Exists(file.StoragePath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(file.StoragePath, cancellationToken);
        return (file, bytes);
    }

    private string SaveBytes(Guid fileId, byte[] bytes, string filename)
    {
        var root = ResolveGeneratedRoot();
        var dir = Path.Combine(root, fileId.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, filename);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string ResolveGeneratedRoot()
    {
        var path = _options.GeneratedFilesPath;
        if (!Path.IsPathRooted(path))
        {
            path = Path.GetFullPath(Path.Combine(_env.ContentRootPath, path));
        }

        Directory.CreateDirectory(path);
        return path;
    }

    private static string BuildJsonPrompt(ExportFormat format, string query, string context)
    {
        var schema = format == ExportFormat.Excel
            ? """
              {
                "filename": "short-descriptive-name.xlsx",
                "sheets": [
                  {
                    "name": "Sheet1",
                    "headers": ["Description", "Hours", "Total"],
                    "rows": [
                      ["Task name", 2, "=B2*35"],
                      ["Another task", 1.5, "=B3*35"]
                    ]
                  }
                ]
              }
              """
            : """
              {
                "filename": "short-descriptive-name.docx",
                "title": "Document title",
                "sections": [
                  {
                    "heading": "Section heading",
                    "paragraphs": ["Paragraph text."]
                  }
                ]
              }
              """;

        return $"""
            Context:
            {context}

            User request: {query}

            Return JSON only matching this schema:
            {schema}
            """;
    }

    private static string ExtractJson(string text)
    {
        text = text.Trim();
        var fenceMatch = Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        if (fenceMatch.Success)
        {
            text = fenceMatch.Groups[1].Value.Trim();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            text = text[start..(end + 1)];
        }

        return text;
    }

    private static string SanitizeFilename(string? proposed, string fallback, string extension)
    {
        var name = string.IsNullOrWhiteSpace(proposed) ? fallback : proposed.Trim();
        name = Path.GetFileName(name);
        name = Regex.Replace(name, @"[^\w\-. ]+", "-");
        if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            name = Path.ChangeExtension(name, extension);
        }

        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
