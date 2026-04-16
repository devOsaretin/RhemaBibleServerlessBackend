

public sealed class PollyTtsOptions
{
    public const string SectionName = "Polly";

    /// <summary>AWS region id, e.g. us-east-1.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Optional; if omitted, the AWS SDK default credential chain is used (env vars, instance profile).</summary>
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    public string DefaultVoiceId { get; set; } = "Joanna";

    /// <summary>neural or standard</summary>
    public string Engine { get; set; } = "neural";
}
