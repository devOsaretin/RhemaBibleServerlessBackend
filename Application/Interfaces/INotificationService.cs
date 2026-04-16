

public interface INotificationService
{
    Task<bool> SendAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default);



}
