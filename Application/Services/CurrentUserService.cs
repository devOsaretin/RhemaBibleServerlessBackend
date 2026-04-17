using System.Security.Claims;

public class CurrentUserService(ICurrentPrincipalAccessor principalAccessor, IUserApplicationService userService) : ICurrentUserService
{
  private readonly ICurrentPrincipalAccessor _principalAccessor = principalAccessor;
  private readonly IUserApplicationService _userService = userService;

  public string GetCurrentUserId()
  {
    var user = _principalAccessor.Principal;
    var authId = user?.GetAuthenticatedUserId();

    return authId ?? throw new UnauthorizedAccessException("UserId not found");
  }

  public async Task<User> GetUserAsync(CancellationToken cancellationToken)
  {

    var authId = GetCurrentUserId();

    if (string.IsNullOrWhiteSpace(authId))
      throw new UnauthorizedAccessException("Clerk ID not found in token");

    var user = await _userService.GetByIdAsync(authId, cancellationToken);

    return user ?? throw new UserNotFoundException(authId);
  }


}
