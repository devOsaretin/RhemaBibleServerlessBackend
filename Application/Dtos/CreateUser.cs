using System.ComponentModel.DataAnnotations;


public record CreateUser(
    [Required(ErrorMessage = "User Auth ID is required")]
    string AuthId,

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email is not valid")]
    string Email,

    string? FirstName = null,

    string? LastName = null,

    string? ImageUrl = null
);
