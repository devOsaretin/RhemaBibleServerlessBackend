
using System.ComponentModel.DataAnnotations;




public record CreateNoteDto(
[Required(ErrorMessage = "Reference is required")]
string Reference,
[Required(ErrorMessage = "Text is required")]
string Text

);
