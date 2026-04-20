using System.ComponentModel.DataAnnotations;

public sealed class ApplyVerseRequest
{
    [Required]
    public string Reference { get; set; } = string.Empty;

    [Required]
    public string VerseText { get; set; } = string.Empty;

    public string? UserNote { get; set; }
}
