

public interface IAIClient
{
    Task<AiClientResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    Task<AiClientResult> GeneratePrayerAsync(string prompt, CancellationToken cancellationToken = default);

    Task<AiClientResult> GenerateChatAsync(string prompt, CancellationToken cancellationToken = default);

    Task<AiClientResult> GenerateApplyVerseAsync(ApplyVerseRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiStreamPart> StreamGenerateAsync(string prompt, CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiStreamPart> StreamGeneratePrayerAsync(string prompt, CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiStreamPart> StreamGenerateChatAsync(string prompt, CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiStreamPart> StreamGenerateApplyVerseAsync(
        ApplyVerseRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiStreamPart> StreamGenerateConversationGospelChatAsync(
        IReadOnlyList<ChatMessageDto> conversationMessages,
        CancellationToken cancellationToken = default);
}
