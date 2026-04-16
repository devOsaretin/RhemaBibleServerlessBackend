

public sealed class AzureBlobTtsOptions
{
    public const string SectionName = "AzureBlobTts";

    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "tts-audio";

 
    public string BlobPrefix { get; set; } = "tts";
    public int SasExpiryMinutes { get; set; } = 60;

    public int MemoryCacheExpirationMinutes { get; set; } = 120;
}
