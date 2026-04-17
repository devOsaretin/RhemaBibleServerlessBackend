using System.ComponentModel.DataAnnotations;

namespace RhemaBibleAppServerless.Domain.Models;

public class Note
{
  public string? Id { get; set; }

  [Required(ErrorMessage = "User AuthId is required")]
  public required string AuthId { get; set; }

  [Required(ErrorMessage = "Reference is required")]
  public required string Reference { get; set; }

  [Required(ErrorMessage = "Text is required")]
  public required string Text { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
