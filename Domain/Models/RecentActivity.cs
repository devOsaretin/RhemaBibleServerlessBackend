using System.ComponentModel.DataAnnotations;
using RhemaBibleAppServerless.Domain.Enums;

namespace RhemaBibleAppServerless.Domain.Models;

public class RecentActivity
{
  public string? Id { get; set; }

  [Required(ErrorMessage = "User AuthId is required")]
  public required string AuthId { get; set; }

  [Required(ErrorMessage = "Title is required")]
  public required string Title { get; set; }

  [Required(ErrorMessage = "Activity type is required")]
  [EnumDataType(typeof(ActivityType), ErrorMessage = "Invalid activity type")]
  public required ActivityType ActivityType { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
