using System.Security.Claims;

public interface ICurrentPrincipalAccessor
{
  ClaimsPrincipal? Principal { get; set; }
}

