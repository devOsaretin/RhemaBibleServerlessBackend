using System.ComponentModel.DataAnnotations;

namespace RhemaBibleAppServerless.Domain.Models;

public class SavedVerse
{
  public string? Id { get; set; }

  [Required(ErrorMessage = "User AuthId is required")]
  public required string AuthId { get; set; }

  [Required(ErrorMessage = "Reference is required")]
  public required string Reference { get; set; }

  [Required(ErrorMessage = "Text is required")]
  public required string Text { get; set; }

  [Required(ErrorMessage = "Verse is required")]
  public required int Verse { get; set; }

  public bool Pilcrow { get; set; }

  public DateTime Created { get; set; } = DateTime.UtcNow;
}
