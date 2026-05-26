using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using project.Models;

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
        private readonly IServiceScopeFactory _scopeFactory;

        // Constructor üzerinden IServiceScopeFactory'i projeye dahil (inject) ediyoruz
        public EmailService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, List<(string FileName, byte[] Content)> attachments = null)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("382termproject@gmail.com", "nqlo lnde jiln dulb"), 
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("382termproject@gmail.com", "Yemek Sipariş Sistemi"), 
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

                // Gönderim başarılıysa veritabanına logla
                await LogEmailEvent("E-Posta Gönderildi", $"Alıcı: {toEmail} | Konu: {subject}");
            }
            catch (Exception ex)
            {
                // Gönderim başarısızsa hatayı veritabanına logla
                await LogEmailEvent("E-Posta Gönderim Hatası", $"Alıcı: {toEmail} | Hata: {ex.Message}");
                throw; // Uygulamanın hatadan haberdar olması için hatayı yukarı fırlatmaya devam et
            }
        }

        // Veritabanı context'ine güvenli şekilde ulaşıp log yazan özel yardımcı metot
        private async Task LogEmailEvent(string actionType, string description)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DenemeContext>();
                
                context.SystemLogs.Add(new SystemLog {
                    ActionType = actionType,
                    Description = description,
                    UserId = null, // E-posta servisi arka planda çalıştığı için kullanıcı ID'sini null geçiyoruz
                    Timestamp = DateTime.Now
                });
                
                await context.SaveChangesAsync();
            }
        }
    }
}