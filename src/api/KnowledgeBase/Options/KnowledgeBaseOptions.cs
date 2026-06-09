namespace Intranet.Api.KnowledgeBase.Options;

public sealed class KnowledgeBaseOptions
{
    public const string SectionName = "KnowledgeBase";

    public string ConnectionString { get; set; } = string.Empty;
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string EmbedModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "llama3.1:8b";
    public string StoragePath { get; set; } = "../etc-kg/data/raw";
    public string IngestStagingPath { get; set; } = "../etc-kg/data/staging";
    /// <summary>When set, uploads go to Azure Blob (required for App Service + VM worker).</summary>
    public string? AzureStorageConnectionString { get; set; }
    public string AzureStorageContainer { get; set; } = "knowledge-raw";
    public string PythonPath { get; set; } = "python";
    public string EtcKgPath { get; set; } = "../etc-kg";
    public string? MigrationSqlPath { get; set; }
    public int SearchTopK { get; set; } = 20;
    public int ChatTopK { get; set; } = 8;
    public double HybridKeywordWeight { get; set; } = 0.25;
    public string GeneratedFilesPath { get; set; } = "../etc-kg/data/generated";
}
