

using System.Net;
using System.Net.Mail;


public class SmtpService : IEmailProvider
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _from;

    public SmtpService(IConfiguration config)
    {
        _host = config["Smtp:Host"] ?? throw new Exception("Smtp HOST is required");
        _port = int.Parse(config["Smtp:Port"]!);
        _username = config["Smtp:Username"] ?? throw new Exception("Smtp host is required");
        _password = config["Smtp:Password"] ?? throw new Exception("Smtp PASSWORDUSERNAME is required");
        _from = config["Smtp:From"] ?? throw new Exception("Smtp FROM is required"); ;
    }

    public async Task<bool> SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_username, _password)
        };

        using var message = new MailMessage(_from, to, subject, body)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(message, cancellationToken);
        return true;
    }
}
