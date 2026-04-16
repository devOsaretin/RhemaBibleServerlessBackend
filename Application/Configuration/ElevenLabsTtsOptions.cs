

public sealed class ElevenLabsVoiceSettings
{
    public double Stability { get; set; } = 0.45;

    public double SimilarityBoost { get; set; } = 0.75;

    public double Style { get; set; }

    public bool UseSpeakerBoost { get; set; } = true;
}

public sealed class ElevenLabsTtsOptions
{
    public const string SectionName = "ElevenLabs";

    public string ApiKey { get; set; } = string.Empty;

    public string DefaultVoiceId { get; set; } = string.Empty;

    public string GrandpaVoiceId { get; set; } = string.Empty;

    public string AsherVoiceId { get; set; } = string.Empty;

    public List<string> GrandpaBooks { get; set; } = new();

    public string DefaultModelId { get; set; } = "eleven_multilingual_v2";

    public string OutputFormat { get; set; } = "mp3_44100_128";

    public string BaseUrl { get; set; } = "https://api.elevenlabs.io/v1";

    public ElevenLabsVoiceSettings VoiceSettings { get; set; } = new();
}
