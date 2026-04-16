

public sealed class TextToSpeechRoutingOptions
{
    public const string SectionName = "Tts";

    /// <summary>
    /// When true, non‑Premium users may use AWS Polly (cost control). Premium subscribers always use ElevenLabs.
    /// </summary>
    public bool UsePollyForNonProSubscribers { get; set; } = true;
}
