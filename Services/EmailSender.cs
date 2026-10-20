using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace SmartScan.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailSender> _logger;

        // Inject ILogger so you can track SMTP errors in your console/logs
        public EmailSender(IConfiguration config, ILogger<EmailSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            try
            {
                var emailSettings = _config.GetSection("EmailSettings");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(emailSettings["SenderName"], emailSettings["SenderEmail"]));
                message.To.Add(MailboxAddress.Parse(email));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlMessage
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();

                // Safely parse the port, fallback to 587 if missing or invalid in config
                int.TryParse(emailSettings["Port"], out int port);
                port = port == 0 ? 587 : port;

                // Connecting to port for STARTTLS
                await client.ConnectAsync(
                    emailSettings["SmtpServer"],
                    port,
                    SecureSocketOptions.StartTls
                );

                // Authenticate with your sender email & app password
                await client.AuthenticateAsync(emailSettings["SenderEmail"], emailSettings["Password"]);

                // Sends the email
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Logs the exact error (e.g., auth failure, connection timeout)
                _logger.LogError(ex, "Failed to send email to {Email}. Subject: {Subject}", email, subject);

            }
        }
    }
}