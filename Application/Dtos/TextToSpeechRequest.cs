

public sealed class TextToSpeechRequest
{
    public required string Text { get; set; }

    public string? Book { get; set; }

    public string? VoiceId { get; set; }

    public string? ModelId { get; set; }
}
