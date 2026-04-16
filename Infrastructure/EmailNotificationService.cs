


public class EmailNotificationService(IEmailProvider emailProvider) : INotificationService
{

    public async Task<bool> SendAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        return await emailProvider.SendEmailAsync(to, subject, body, cancellationToken);
    }
}

