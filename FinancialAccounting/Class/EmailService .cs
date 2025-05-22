using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinancialAccounting
{
    public class EmailService : IEmailService
    {
        public void SendEmail(string toEmail, string filePath)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Финансовый учёт", "misha.barinow2016@yandex.ru"));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = "Ваш финансовый отчёт";

            var builder = new BodyBuilder
            {
                TextBody = $"Здравствуйте!\n\nВаш финансовый отчёт от {DateTime.Now:dd.MM.yyyy} во вложении."
            };
            builder.Attachments.Add(filePath);
            message.Body = builder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                client.Connect("smtp.yandex.ru", 587, MailKit.Security.SecureSocketOptions.StartTls);
                client.Authenticate("misha.barinow2016@yandex.ru", "tenftaiqbsgdqxlf");
                client.Send(message);
                client.Disconnect(true);
            }
        }
    }
}
