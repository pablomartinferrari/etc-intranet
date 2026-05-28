namespace Intranet.Api.MultifamilyLbp.Models;

public record EntityDashboardDto(
    string JobId,
    string EntitySlug,
    string EntityDisplayName,
    int UploadedFilesCount,
    int UnitsRowCount,
    int CommonAreasRowCount,
    int ValidationWarningCount,
    int ValidationErrorCount,
    int PendingNormalizationCount,
    string? NormalizationStatus,
    DateTimeOffset? LastReportGeneratedAt,
    bool HasRows);

public record UploadBatchDto(
    Guid Id,
    string SourceFileName,
    string DataType,
    string Status,
    int ImportedRowCount,
    string? BuildingProperty,
    string? BatchName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string>? Warnings);

public record UploadResultDto(
    Guid BatchId,
    string SourceFileName,
    string DataType,
    string Status,
    int RowCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

public record InspectionRowDto(
    Guid Id,
    string ReadingId,
    string SourceFileName,
    string DataType,
    string Location,
    string? RoomOrArea,
    string Component,
    string? NormalizedComponent,
    string? Substrate,
    string? NormalizedSubstrate,
    int ShotCount,
    string? Notes,
    string ValidationStatus,
    string Color,
    double LeadContent,
    bool IsPositive,
    string? Side);

public record NormalizationSuggestionDto(
    Guid Id,
    string FieldName,
    string OriginalValue,
    string SuggestedValue,
    string? ApprovedValue,
    int AffectedRowCount,
    string DataType,
    string Confidence,
    string Status);

public record RunNormalizationRequest(
    IReadOnlyList<string> Fields,
    string Scope,
    string? DataType,
    IReadOnlyList<Guid>? RowIds);

public record ApplyNormalizationRequest(IReadOnlyList<Guid> SuggestionIds);

public record ReportConfigRequest(
    string DataType,
    IReadOnlyList<string> Sections,
    double UniformThreshold,
    string GroupBy,
    bool UseNormalizedValues);

public record ReportSnapshotDto(
    Guid Id,
    string DataType,
    double UniformThreshold,
    bool UseNormalizedValues,
    DateTimeOffset GeneratedAt,
    string? GeneratedBy,
    object Result);

public record PatchRowsRequest(IReadOnlyList<InspectionRowPatch> Rows);

public record InspectionRowPatch(
    Guid Id,
    string? Location,
    string? RoomOrArea,
    string? Component,
    string? NormalizedComponent,
    string? Substrate,
    string? NormalizedSubstrate,
    int? ShotCount,
    string? Notes);

public record ImportLegacyRequest(bool Overwrite = false);
