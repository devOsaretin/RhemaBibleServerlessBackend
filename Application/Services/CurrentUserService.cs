using System.Security.Claims;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor, IUserService userService) : ICurrentUserService
{
  private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
  private readonly IUserService _userService = userService;

  public string GetCurrentUserId()
  {
    var context = _httpContextAccessor.HttpContext;
    var authId = context?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

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
