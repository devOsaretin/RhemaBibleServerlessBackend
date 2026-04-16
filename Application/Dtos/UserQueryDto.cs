

public class UserQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; }
    public string? SubscriptionType { get; set; }
    public string? Search { get; set; }
}

