using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Project.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        // Returns true if sent, false otherwise. 'error' contains exception details when false.
        public bool Send(string to, string subject, string body, out string? error)
        {
            error = null;
            var smtp = _config.GetSection("Smtp");
            var host = smtp.GetValue<string>("Host");
            var port = smtp.GetValue<int>("Port");
            var username = smtp.GetValue<string>("Username");
            var password = smtp.GetValue<string>("Password");
            var from = smtp.GetValue<string>("From");
            var enableSsl = smtp.GetValue<bool>("EnableSsl");

            try
            {
                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(from));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = body };
                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                // Accept all SSL certificates (for dev). In production, remove this option.
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                SecureSocketOptions options = SecureSocketOptions.StartTlsWhenAvailable;
                if (!enableSsl)
                    options = SecureSocketOptions.None;

                client.Connect(host, port, options);

                if (!string.IsNullOrEmpty(username))
                {
                    client.Authenticate(username, password);
                }

                client.Send(message);
                client.Disconnect(true);

                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.ToString();
                Console.WriteLine("EmailService Exception: " + error);
                return false;
            }
        }
    }
}
