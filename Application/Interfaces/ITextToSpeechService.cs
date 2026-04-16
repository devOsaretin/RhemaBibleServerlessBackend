

public interface ITextToSpeechService
{
    Task<TextToSpeechResponse> SynthesizeAsync(TextToSpeechRequest request, CancellationToken cancellationToken = default);
}
