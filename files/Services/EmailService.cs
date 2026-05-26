using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace project.Services
{
    // 1. Arayüz (Interface) - attachments parametresi buraya da eklendi
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body, List<(string FileName, byte[] Content)> attachments = null);
    }

    // 2. Servis Sınıfı
    public class EmailService : IEmailService
    {
        public async Task SendEmailAsync(string toEmail, string subject, string body, List<(string FileName, byte[] Content)> attachments = null)
        {
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("382termproject@gmail.com", "nqlo lnde jiln dulb"), // Burayı kendi bilgilerinle doldurmayı unutma
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("382termproject@gmail.com", "Yemek Sipariş Sistemi"), // Burayı da kendi mailinle değiştir
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(toEmail);

            // Eğer e-postaya eklenmiş dosyalar (PDF'ler) varsa dönüştürüp ekliyoruz
            if (attachments != null && attachments.Count > 0)
            {
                foreach (var att in attachments)
                {
                    var stream = new MemoryStream(att.Content);
                    var mailAttachment = new Attachment(stream, att.FileName, "application/pdf");
                    mailMessage.Attachments.Add(mailAttachment);
                }
            }

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}