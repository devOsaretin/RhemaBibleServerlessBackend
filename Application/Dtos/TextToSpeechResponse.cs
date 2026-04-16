

public sealed class TextToSpeechResponse
{
       public required string AudioUrl { get; set; }


    public required string ContentHash { get; set; }

 
    public bool ServedFromCache { get; set; }

    
    public required string Source { get; set; }

    
    public string? CacheSource { get; set; }

    public string MimeType { get; set; } = "audio/mpeg";
}
