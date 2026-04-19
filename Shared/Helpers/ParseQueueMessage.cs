using System.Text.Json;

public static class ParseQueueMessage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public static T Parse<T>(string message)
    {
        if (string.IsNullOrEmpty(message)) throw new InvalidOperationException("Missing queue message body");

        try
        {
            var dto = JsonSerializer.Deserialize<T>(message, Options);
            return dto ?? throw new JsonException("Deserialized Queue payload was null.");
        }
        catch (JsonException ex)
        {

            throw new InvalidOperationException($"Failed to deserialize queue payload, Message: {message}", ex);
        }


    }
}