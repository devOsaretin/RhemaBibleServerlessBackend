

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class ForgotPasswordRequest
{
    public required string Email { get; set; }
}

public class ChangePasswordRequest
{
    public required string OldPassword { get; set; }
    public required string Password { get; set; }
}