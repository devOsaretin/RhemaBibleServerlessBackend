namespace RhemaBibleAppServerless.Application.Persistence;

public sealed class AdminUserListQuery
{
  public int PageNumber { get; init; }
  public int PageSize { get; init; }
  public string? Status { get; init; }
  public string? SubscriptionType { get; init; }
  public string? Search { get; init; }
}
