using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using project.Models;
using iText.Html2pdf;
using System.IO;

namespace project.Controllers
{
    [Authorize] // Giriş yapmamış kimse bu sayfaya erişemez
    public class CustomerController : Controller
    {
        private readonly DenemeContext _context;

        public CustomerController(DenemeContext context)
        {
            _context = context;
        }

        // Müşteri Paneli ve Sipariş Geçmişi
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            
            // Sisteme giriş yapmış olan müşterinin ID'sini alıyoruz
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Auth");
            
            int userId = int.Parse(userIdStr);

            // Müşterinin tüm geçmiş siparişlerini tedarikçi bilgisiyle birlikte çekiyoruz
            var myOrders = await _context.Orders
                .Include(o => o.Caterer)
                .Include(o => o.Ratings)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // İstatistikleri hesaplayıp View'a gönderiyoruz
            ViewBag.TotalOrders = myOrders.Count;
            ViewBag.TotalSpent = myOrders.Sum(o => o.TotalAmount); // C# null hatası almamak için sade kullanım
            
            // "Tamamlandı" statüsündeki siparişler
            ViewBag.CompletedOrders = myOrders.Count(o => o.Status == "Tamamlandı");

            return View(myOrders);
        }
        [HttpGet]
        [HttpGet]
public async Task<IActionResult> DownloadReceipt(int id)
{
    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Auth");
    int userId = int.Parse(userIdStr);

    // Siparişi; ürünleri, ürün tanımlarını ve tüm menü seçeneklerini (customizations) içerecek şekilde çekiyoruz
    var order = await _context.Orders
        .Include(o => o.Caterer)
        .Include(o => o.User)
        .Include(o => o.OrderItems).ThenInclude(oi => oi.Item)
        .Include(o => o.OrderItems).ThenInclude(oi => oi.OrderItemOptions).ThenInclude(oio => oio.Option)
        .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

    if (order == null) return NotFound("Sipariş bulunamadı.");

    // Detaylı Fiş İçin Dinamik HTML Yapısı Oluşturma
    var htmlBuilder = new System.Text.StringBuilder();
    htmlBuilder.Append($@"
    <html>
    <head>
        <meta charset='utf-8' />
        <style>
            body {{ font-family: 'Arial', sans-serif; color: #333; padding: 10px; }}
            .receipt-box {{ border: 1px solid #ddd; padding: 25px; border-radius: 8px; max-width: 750px; margin: auto; }}
            .header {{ text-align: center; border-bottom: 3px double #dee2e6; padding-bottom: 15px; margin-bottom: 20px; }}
            .company-name {{ font-size: 22px; font-weight: bold; color: #198754; text-transform: uppercase; }}
            .title {{ font-size: 14px; color: #6c757d; margin-top: 5px; font-weight: bold; }}
            .info-table {{ width: 100%; margin-bottom: 25px; border-collapse: collapse; }}
            .info-table td {{ padding: 4px 0; font-size: 13px; color: #495057; }}
            .items-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
            .items-table th {{ background-color: #f8f9fa; border-top: 1px solid #dee2e6; border-bottom: 2px solid #dee2e6; padding: 10px; text-align: left; font-size: 13px; font-weight: bold; }}
            .items-table td {{ border-bottom: 1px solid #dee2e6; padding: 12px 10px; font-size: 13px; vertical-align: top; }}
            .customization {{ font-size: 11px; color: #6c757d; margin-top: 5px; padding-left: 8px; border-left: 2px solid #ffc107; font-style: italic; }}
            .total-box {{ text-align: right; margin-top: 25px; padding-top: 15px; border-top: 1px solid #dee2e6; font-size: 16px; font-weight: bold; color: #198754; }}
        </style>
    </head>
    <body>
        <div class='receipt-box'>
            <div class='header'>
                <div class='company-name'>{order.Caterer?.FullName}</div>
                <div class='title'>SİPARİŞ BİLGİ FİŞİ / MAKBUZ</div>
            </div>
            
            <table class='info-table'>
                <tr>
                    <td><strong>Sipariş No:</strong> #{order.OrderId}</td>
                    <td style='text-align: right;'><strong>Tarih:</strong> {order.OrderDate?.ToString("dd.MM.yyyy HH:mm")}</td>
                </tr>
                <tr>
                    <td><strong>Müşteri:</strong> {order.User?.FullName}</td>
                    <td style='text-align: right;'><strong>E-Posta:</strong> {order.User?.Email}</td>
                </tr>
            </table>

            <table class='items-table'>
                <thead>
                    <tr>
                        <th>Ürün Açıklaması / Seçenekler</th>
                        <th style='text-align: center; width: 60px;'>Adet</th>
                        <th style='text-align: right; width: 110px;'>Birim Fiyat</th>
                        <th style='text-align: right; width: 110px;'>Toplam Tutar</th>
                    </tr>
                </thead>
                <tbody>");

    // Alınan her ürünü tek tek dönerek tabloya ekliyoruz
    foreach (var item in order.OrderItems)
    {
        var optionsText = "";
        if (item.OrderItemOptions != null && item.OrderItemOptions.Any())
        {
            // Ürüne eklenmiş olan ekstra customization opsiyonlarını string olarak birleştiriyoruz
            var optionsList = item.OrderItemOptions.Select(o => $"{o.Option?.Name} (+{o.Option?.PriceChange:C2})");
            optionsText = $"<div class='customization'>Seçenekler: {string.Join(", ", optionsList)}</div>";
        }

        var lineTotal = item.Quantity * item.UnitPrice;

        htmlBuilder.Append($@"
            <tr>
                <td>
                    <span style='font-weight: bold; color: #212529;'>{item.Item?.Title}</span>
                    {optionsText}
                </td>
                <td style='text-align: center; font-weight: bold;'>{item.Quantity}</td>
                <td style='text-align: right; color: #6c757d;'>{item.UnitPrice:C2}</td>
                <td style='text-align: right; font-weight: bold; color: #212529;'>{lineTotal:C2}</td>
            </tr>");
    }

    htmlBuilder.Append($@"
                </tbody>
            </table>

            <div class='total-box'>
                GENEL TOPLAM: {order.TotalAmount:C2}
            </div>
        </div>
    </body>
    </html>");

    // Oluşturulan detaylı HTML şablonunu iText aracılığıyla PDF dosyasına dönüştürüyoruz
    using (var stream = new MemoryStream())
    {
        iText.Html2pdf.HtmlConverter.ConvertToPdf(htmlBuilder.ToString(), stream);
        return File(stream.ToArray(), "application/pdf", $"Siparis_Fisi_{order.OrderId}.pdf");
    }
}

        // 2. Dinamik Sözleşme (Agreement) Üretimi ve İndirilmesi
        [HttpGet]
        public async Task<IActionResult> DownloadAgreement(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            
            var order = await _context.Orders
                .Include(o => o.Caterer)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

            if (order == null) return NotFound();

            // Sözleşme HTML Şablonu
            string html = $@"
            <div style='font-family: Helvetica, sans-serif; padding: 30px; line-height: 1.6; color: #333;'>
                <h2 style='text-align: center; text-decoration: underline;'>MESAFELİ SATIŞ SÖZLEŞMESİ</h2>
                <br/>
                <p><strong>MADDE 1 - TARAFLAR</strong></p>
                <p><strong>Satıcı (Tedarikçi):</strong> {order.Caterer?.FullName} <br/>
                   <strong>Alıcı (Müşteri):</strong> {order.User?.FullName}</p>
                <br/>
                <p><strong>MADDE 2 - SÖZLEŞMENİN KONUSU</strong></p>
                <p>İşbu sözleşmenin konusu, Alıcı'nın Satıcı'ya ait platform üzerinden elektronik ortamda siparişini yaptığı <strong>{order.OrderDate?.ToString("dd.MM.yyyy")}</strong> tarihli ve <strong>#{order.OrderId}</strong> numaralı siparişin satışı, hazırlanması ve teslimi ile ilgili yasal hak ve yükümlülüklerin 6502 sayılı Tüketicinin Korunması Hakkında Kanun hükümleri gereğince belirlenmesidir.</p>
                <br/>
                <p><strong>MADDE 3 - SİPARİŞ TUTARI VE ÖDEME</strong></p>
                <p>Siparişin toplam vergiler dahil tutarı <strong>{order.TotalAmount.ToString("C2")}</strong> olarak belirlenmiş ve elektronik ortamda kredi kartı simülasyonu ile tahsil edilmiştir.</p>
                <br/>
                <p style='margin-top: 50px; text-align: center; font-style: italic;'>İşbu sözleşme elektronik ortamda onaylanmış olup hukuken geçerlidir.</p>
            </div>";

            using (var stream = new MemoryStream())
            {
                HtmlConverter.ConvertToPdf(html, stream);
                return File(stream.ToArray(), "application/pdf", $"Sozlesme_{order.OrderId}.pdf");
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> SubmitRating(int orderId, int menuScore, int catererScore, int service, int speed, string comment)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Auth");
            
            int userId = int.Parse(userIdStr);

            // Siparişi ve doğruluğunu kontrol et
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

            if (order == null || order.Status != "Tamamlandı")
            {
                TempData["Error"] = "Sadece tamamlanmış kendi siparişlerinize puan verebilirsiniz.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Yeni SQL Tablona göre Rating objesini oluştur
            var rating = new Rating
            {
                OrderId = order.OrderId,
                MenuScore = menuScore,
                CatererScore = catererScore,
                Service = service,
                Speed = speed,
                Comment = comment,
                CreatedAt = DateTime.Now // SQL'deki default getDate() olsa da C# tarafından da atamak güvenlidir
            };

            _context.Ratings.Add(rating);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Değerlendirmeniz başarıyla kaydedildi. Geri bildiriminiz için teşekkürler!";
            return RedirectToAction(nameof(Dashboard));
        }
        
        
    }
}