
using System.ComponentModel.DataAnnotations;



public record AddActivityDto(
    [Required(ErrorMessage = "Title is required")]
    string Title,

    [Required(ErrorMessage = "Activity type is required")]
    [EnumDataType(typeof(ActivityType), ErrorMessage = "Invalid activity type")]
    ActivityType ActivityType
);

public class AddActivityToQueueDto
{
    public required string Title { get; set; }
    public required string ActivityType { get; set; }
    public required string AuthId { get; set; }
}