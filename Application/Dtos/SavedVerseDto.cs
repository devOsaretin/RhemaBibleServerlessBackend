using System.ComponentModel.DataAnnotations;



public record SavedVerseDto(
    [Required(ErrorMessage = "Reference is required")]
    string Reference,

    [Required(ErrorMessage = "Text is required")]
    string Text,

    int Verse,

    bool Pilcrow = false
);
