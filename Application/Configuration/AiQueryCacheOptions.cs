

public sealed class AiQueryCacheOptions
{
    public const string SectionName = "AiQueryCache";

    public bool Enabled { get; set; } = true;

    public int ExpirationHours { get; set; } = 24;
}
