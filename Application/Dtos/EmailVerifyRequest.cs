

public class EmailVerifyRequest
{
    public required string Otp { get; set; }
    public required string Email { get; set; }
}

public record EmailRequestFromQueueDto
{
    public required string Recipient {get; set;}
    public required string Subject {get; set;}
    public required string Body {get; set;}
}