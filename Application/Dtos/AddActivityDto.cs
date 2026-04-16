
using System.ComponentModel.DataAnnotations;



public record AddActivityDto(
    [Required(ErrorMessage = "Title is required")]
    string Title,

    [Required(ErrorMessage = "Activity type is required")]
    [EnumDataType(typeof(ActivityType), ErrorMessage = "Invalid activity type")]
    ActivityType ActivityType
);

