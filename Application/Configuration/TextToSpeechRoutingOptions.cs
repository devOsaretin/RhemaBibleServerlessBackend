

public sealed class TextToSpeechRoutingOptions
{
    public const string SectionName = "Tts";
    public bool UsePollyForNonProSubscribers { get; set; } = true;
}
