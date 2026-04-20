public static class ApplyVerseRequestValidator
{
    public const int MaxReferenceLength = 120;
    public const int MaxVerseTextLength = 2000;
    public const int MaxUserNoteLength = 800;

    public static (string Reference, string VerseText, string? UserNote) NormalizeOrThrow(ApplyVerseRequest? request)
    {
        if (request is null)
            throw new BadRequestException("Request body is required.");

        var reference = (request.Reference ?? string.Empty).Trim();
        var verseText = (request.VerseText ?? string.Empty).Trim();

        if (reference.Length == 0)
            throw new BadRequestException("Reference is required.");

        if (reference.Length > MaxReferenceLength)
            throw new BadRequestException($"Reference may be at most {MaxReferenceLength} characters.");

        if (verseText.Length == 0)
            throw new BadRequestException("VerseText is required.");

        if (verseText.Length > MaxVerseTextLength)
            throw new BadRequestException($"VerseText may be at most {MaxVerseTextLength} characters.");

        string? userNote = string.IsNullOrWhiteSpace(request.UserNote) ? null : request.UserNote.Trim();
        if (userNote is not null && userNote.Length > MaxUserNoteLength)
            throw new BadRequestException($"UserNote may be at most {MaxUserNoteLength} characters.");

        return (reference, verseText, userNote);
    }
}
