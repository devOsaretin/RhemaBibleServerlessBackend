using System.Security.Claims;

public sealed class AsyncLocalCurrentPrincipalAccessor : ICurrentPrincipalAccessor
{
  private static readonly AsyncLocal<ClaimsPrincipal?> Current = new();

  public ClaimsPrincipal? Principal
  {
    get => Current.Value;
    set => Current.Value = value;
  }
}

