public static class ConversationGospelChatValidator
{
    public const int MaxMessages = 24;
    public const int MaxContentLength = 4000;

    public static IReadOnlyList<ChatMessageDto> NormalizeOrThrow(IReadOnlyList<ChatMessageDto>? messages)
    {
        if (messages is null || messages.Count == 0)
            throw new BadRequestException("At least one message is required.");

        if (messages.Count > MaxMessages)
            throw new BadRequestException($"At most {MaxMessages} messages are allowed.");

        var result = new List<ChatMessageDto>(messages.Count);
        foreach (var m in messages)
        {
            if (m is null || string.IsNullOrWhiteSpace(m.Role))
                throw new BadRequestException("Each message must have a role.");

            var role = m.Role.Trim().ToLowerInvariant();
            if (role is not ("user" or "assistant"))
                throw new BadRequestException("Message roles must be \"user\" or \"assistant\".");

            if (string.IsNullOrWhiteSpace(m.Content))
                throw new BadRequestException("Each message must have non-empty content.");

            var content = m.Content.Trim();
            if (content.Length > MaxContentLength)
                throw new BadRequestException($"Each message may be at most {MaxContentLength} characters.");

            result.Add(new ChatMessageDto { Role = role, Content = content });
        }

        if (!string.Equals(result[0].Role, "user", StringComparison.Ordinal))
            throw new BadRequestException("The conversation must begin with a user message.");

        for (var i = 1; i < result.Count; i++)
        {
            if (string.Equals(result[i].Role, result[i - 1].Role, StringComparison.Ordinal))
                throw new BadRequestException("User and assistant messages must alternate.");
        }

        if (!string.Equals(result[^1].Role, "user", StringComparison.Ordinal))
            throw new BadRequestException("The last message must be from the user.");

        return result;
    }
}
