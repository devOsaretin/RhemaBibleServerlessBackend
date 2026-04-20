using System.ComponentModel.DataAnnotations;

public sealed class ConversationGospelChatStreamRequest
{
    [Required]
    [MinLength(1)]
    public List<ChatMessageDto> Messages { get; set; } = new();
}
