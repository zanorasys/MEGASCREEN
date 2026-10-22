using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MegaScreen.Web.Services;

public sealed class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "Mega Screen";
}

public interface IEmailSender
{
    bool Enabled { get; }
    Task SendAsync(string to, string subject, string html);
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _o = options.Value;
    public bool Enabled => _o.Enabled && !string.IsNullOrWhiteSpace(_o.Host) && !string.IsNullOrWhiteSpace(_o.From);

    public async Task SendAsync(string to, string subject, string html)
    {
        if (!Enabled) return;
        using var client = new SmtpClient(_o.Host, _o.Port)
        {
            EnableSsl = _o.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_o.User) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(_o.User, _o.Password)
        };
        using var msg = new MailMessage
        {
            From = new MailAddress(_o.From, _o.FromName),
            Subject = subject,
            Body = html,
            IsBodyHtml = true
        };
        msg.To.Add(to);
        await client.SendMailAsync(msg);
    }
}
