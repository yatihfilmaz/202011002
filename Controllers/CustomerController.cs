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
        public async Task<IActionResult> CatererMenu(int id, int? categoryId)
        {
            // 1. Tedarikçi (Restoran) Bilgilerini Çekme
            var caterer = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
            ViewBag.CatererName = caterer?.FullName ?? "Restoran";
            ViewBag.CatererId = id;

            // 2. Sol Menü İçin Kategorileri Çekme
            ViewBag.Categories = await _context.Categories.ToListAsync();

            // 3. Restorana Ait Tüm Sipariş Değerlendirmelerini (Yorumları) Çekme
            var reviews = await _context.Ratings
                .Include(r => r.Order).ThenInclude(o => o.User)
                .Include(r => r.Order).ThenInclude(o => o.OrderItems).ThenInclude(oi => oi.Item)
                .Where(r => r.Order.CatererId == id)
                .ToListAsync();

            ViewBag.TotalReviews = reviews.Count;
            ViewBag.Reviews = reviews;

            // 4. İstatistiksel Puan Ortalamalarını Hesaplama
            if (reviews.Any())
            {
                ViewBag.AvgMenu = reviews.Average(r => r.MenuScore).ToString("0.0");
                ViewBag.AvgCaterer = reviews.Average(r => r.CatererScore).ToString("0.0");
                ViewBag.AvgService = reviews.Average(r => r.Service).ToString("0.0");
                ViewBag.AvgSpeed = reviews.Average(r => r.Speed).ToString("0.0");
            }
            else
            {
                // Eğer henüz hiç yorum yapılmadıysa varsayılan değerler
                ViewBag.AvgMenu = "0.0";
                ViewBag.AvgCaterer = "0.0";
                ViewBag.AvgService = "0.0";
                ViewBag.AvgSpeed = "0.0";
            }

            // 5. Yemek Listesini ve Yemeklerin Kendi Tekil Puanlarını Çekme
            var menuItems = await _context.MenuItems
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.ItemRatings) // Yemeklerin yanındaki puanların görünmesini sağlayan kritik satır
                .Where(m => m.CatererId == id)
                .ToListAsync();

            // Kategoriye göre filtreleme şartı
            if (categoryId.HasValue)
            {
                menuItems = menuItems.Where(m => m.CategoryId == categoryId).ToList();
            }

            ViewBag.SelectedCategoryId = categoryId;
    
            return View(menuItems);
        }
        
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            try 
            {
                // 1. Kullanıcı ID'sini oturumdan (Cookie) okumaya çalış
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
                if (string.IsNullOrEmpty(userIdStr)) 
                {
                    return Content("HATA 1: Oturumunuz açık fakat sistem ID bilginizi okuyamıyor. AuthController'daki Login (Giriş) kısmında 'ClaimTypes.NameIdentifier' atandığından emin olun.");
                }

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Content($"HATA 2: ID numaraya çevrilemedi. Gelen Veri: {userIdStr}");
                }

                // 2. Kullanıcıyı Veritabanında Bul
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        
                if (user == null)
                {
                    return Content("HATA 3: Veritabanında bu ID'ye sahip bir kullanıcı bulunamadı.");
                }

                // Her şey yolundaysa Arayüzü (View) getir
                return View(user);
            }
            catch (Exception ex)
            {
                // Beklenmeyen bir çökme varsa ekrana doğrudan hatayı yazdır
                return Content($"BEKLENMEYEN SİSTEM HATASI: {ex.Message}");
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Siparişi tüm alt ilişkileriyle (ürünler, opsiyonlar, restoran) birlikte çekiyoruz
            var order = await _context.Orders
                .Include(o => o.Caterer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Item)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemOptions)
                .ThenInclude(oio => oio.Option)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

            if (order == null) return NotFound();

            // 1. Bu siparişe daha önce GENEL puan verilmiş mi kontrol ediyoruz
            var existingRating = await _context.Ratings.FirstOrDefaultAsync(r => r.OrderId == id);
            ViewBag.ExistingRating = existingRating;

            // 2. YENİ EKLENEN KISIM: Bu siparişteki TEKİL YEMEKLERE verilmiş puanları çekiyoruz
            ViewBag.ItemRatings = await _context.ItemRatings.Where(r => r.OrderId == id).ToListAsync();

            return PartialView("_CustomerOrderDetailsPartial", order);
        }
        
        [HttpGet]
public async Task<IActionResult> ViewReceipt(int id)
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
    
    var order = await _context.Orders
        .Include(o => o.User)
        .Include(o => o.Caterer)
        .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

    if (order == null) return NotFound();

    var orderItems = await _context.OrderItems
        .Include(oi => oi.Item) 
        .Include(oi => oi.OrderItemOptions)
            .ThenInclude(oio => oio.Option)
                .ThenInclude(o => o.Item)
        .Where(oi => oi.OrderId == id)
        .ToListAsync();

    string itemsHtml = "";
    if (orderItems != null && orderItems.Any())
    {
        foreach (var item in orderItems)
        {
            decimal subTotal = item.Quantity * item.UnitPrice;
            var itemName = item.Item?.Title ?? "Bilinmeyen Ürün";
            
            string optionsHtml = "";
            if (item.OrderItemOptions != null && item.OrderItemOptions.Any())
            {
                optionsHtml += "<ul style='margin: 5px 0 0 15px; padding-left: 10px; font-size: 0.85em; color: #666;'>";
                foreach (var opt in item.OrderItemOptions)
                {
                    var subItemTitle = opt.Option?.Item?.Title;
                    var optGroup = opt.Option?.OptionGroup;
                    var baseOptName = opt.Option?.Name ?? "Özelleştirme";
                    
                    var optName = !string.IsNullOrEmpty(subItemTitle) 
                        ? $"{subItemTitle} ({optGroup}): {baseOptName}" 
                        : $"{optGroup}: {baseOptName}";

                    var optPriceText = opt.Option?.PriceChange > 0 ? $" (+{opt.Option.PriceChange.Value.ToString("C2")})" : "";
                    optionsHtml += $"<li style='margin-bottom: 3px;'>{optName}{optPriceText}</li>";
                }
                optionsHtml += "</ul>";
            }

            itemsHtml += $@"
            <tr>
                <td style='border: 1px solid #ddd; padding: 12px; text-align: left;'>
                    <div style='font-weight: bold; font-size: 1.1em;'>{itemName}</div>
                    {optionsHtml}
                </td>
                <td style='border: 1px solid #ddd; padding: 12px; text-align: center;'>{item.UnitPrice.ToString("C2")}</td>
                <td style='border: 1px solid #ddd; padding: 12px; text-align: center;'>{item.Quantity}</td>
                <td style='border: 1px solid #ddd; padding: 12px; text-align: right;'>{subTotal.ToString("C2")}</td>
            </tr>";
        }
    }

    string receiptHtml = $@"
    <!DOCTYPE html>
    <html lang='tr'>
    <head>
        <meta charset='UTF-8'>
        <style>
            body {{ font-family: 'Helvetica', 'Arial', sans-serif; color: #333; line-height: 1.5; }}
            .invoice-box {{ max-width: 800px; margin: auto; padding: 30px; border: 1px solid #eee; box-shadow: 0 0 10px rgba(0, 0, 0, 0.15); }}
            .header {{ text-align: center; border-bottom: 2px solid #0d6efd; padding-bottom: 10px; margin-bottom: 20px; }}
            .header h2 {{ color: #0d6efd; margin: 0; }}
            .info-table {{ width: 100%; margin-bottom: 30px; }}
            .info-table td {{ padding: 5px; vertical-align: top; }}
            .items-table {{ width: 100%; border-collapse: collapse; margin-bottom: 20px; }}
            .items-table th, .items-table td {{ border: 1px solid #ddd; padding: 12px; text-align: left; }}
            .items-table th {{ background-color: #f8f9fa; color: #0d6efd; font-weight: bold; }}
            .totals-table {{ width: 100%; margin-top: 20px; }}
            .totals-table td {{ text-align: right; padding: 8px; }}
            .total-row {{ font-size: 1.3em; font-weight: bold; color: #0d6efd; }}
            .footer {{ text-align: center; margin-top: 40px; font-size: 0.8em; color: #999; border-top: 1px solid #eee; padding-top: 15px; }}
        </style>
    </head>
    <body>
        <div class='invoice-box'>
            <div class='header'>
                <h2>Sipariş Faturası & Özeti</h2>
                <p style='margin: 5px 0; color: #888; font-size: 12px;'>Fatura Yerine Geçmez</p>
                <p style='margin: 5px 0;'>Sipariş Numarası: <strong>#{order.OrderId}</strong></p>
            </div>

            <table class='info-table'>
                <tr>
                    <td>Müşteri:<br><strong>{order.User?.FullName}</strong></td>
                    <td style='text-align: right;'>Tarih:<br><strong>{order.OrderDate?.ToString("dd.MM.yyyy HH:mm")}</strong></td>
                </tr>
                <tr>
                    <td>Restoran:<br><strong>{order.Caterer?.FullName}</strong></td>
                    <td style='text-align: right;'>Sipariş Durumu:<br><strong>{order.Status}</strong></td>
                </tr>
            </table>

            <table class='items-table'>
                <thead>
                    <tr>
                        <th>Ürün Detayı</th>
                        <th style='text-align: center;'>Birim Fiyat</th>
                        <th style='text-align: center;'>Adet</th>
                        <th style='text-align: right;'>Ara Toplam</th>
                    </tr>
                </thead>
                <tbody>{itemsHtml}</tbody>
            </table>

            <table class='totals-table'>
                <tr class='total-row'>
                    <td>Genel Toplam:</td>
                    <td style='width: 150px;'>{order.TotalAmount.ToString("C2")}</td>
                </tr>
            </table>

            <div class='footer'>
                &copy; {DateTime.Now.Year} Project Yönetim Sistemi
            </div>
        </div>
    </body>
    </html>";

    using (var stream = new MemoryStream())
    {
        HtmlConverter.ConvertToPdf(receiptHtml, stream);
        // CRITICAL: Dosya ismi parametresi vermeyerek indirmeyi önlüyoruz. Tarayıcıda inline açılır.
        return File(stream.ToArray(), "application/pdf");
    }
}

// 2. Sözleşme Görüntüleme Aksiyonu (Yeni Sekmede Tarayıcı İçi Açılır)
[HttpGet]
public async Task<IActionResult> ViewAgreement(int id)
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
    
    var order = await _context.Orders
        .Include(o => o.User)
        .Include(o => o.Caterer)
        .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

    if (order == null) return NotFound();

    string agreementHtml = $@"
    <div style='font-family: Helvetica, sans-serif; padding: 30px; line-height: 1.6; color: #333;'>
        <h2 style='text-align: center; text-decoration: underline;'>MESAFELİ SATIŞ SÖZLEŞMESİ</h2>
        <br/>
        <p><strong>MADDE 1 - TARAFLAR</strong></p>
        <p><strong>Satıcı (Tedarikçi):</strong> {order.Caterer?.FullName} <br/>
           <strong>Alıcı (Müşteri):</strong> {order.User?.FullName}</p>
        <br/>
        <p><strong>MADDE 2 - SÖZLEŞMENİN KONUSU</strong></p>
        <p>İşbu sözleşmenin konusu, Alıcı'nın Satıcı'ya ait platform üzerinden elektronik ortamda siparişini yaptığı <strong>{order.OrderDate?.ToString("dd.MM.yyyy")}</strong> tarihli ve <strong>#{order.OrderId}</strong> numaralı siparişin satışı ile ilgili yasal hakların belirlenmesidir.</p>
        <br/>
        <p><strong>MADDE 3 - SİPARİŞ TUTARI</strong></p>
        <p>Siparişin toplam vergiler dahil tutarı <strong>{order.TotalAmount.ToString("C2")}</strong> olarak tahsil edilmiştir.</p>
    </div>";

    using (var stream = new MemoryStream())
    {
        HtmlConverter.ConvertToPdf(agreementHtml, stream);
        // İndirmeden tarayıcıda inline gösterim sağlar
        return File(stream.ToArray(), "application/pdf");
    }
}
        
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
        public async Task<IActionResult> RateSingleItem(int ordId, int itmId, int scr)
        {
            int usrId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
    
            // Daha önce puan verilmiş mi kontrolü
            bool exists = await _context.ItemRatings.AnyAsync(r => r.OrderId == ordId && r.ItemId == itmId);
            if(exists) return Json(new { success = false, msg = "Bu yemeği zaten puanladınız." });

            var rt = new ItemRating { 
                OrderId = ordId, 
                ItemId = itmId, 
                UserId = usrId, 
                Score = scr, 
                CreatedAt = DateTime.Now 
            };
    
            _context.ItemRatings.Add(rt);
            await _context.SaveChangesAsync();

            return Json(new { success = true, msg = "Puanınız kaydedildi!" });
        }
        
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRating(int orderId, int menuScore, int catererScore, int service, int speed, string comment)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
    
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);
            if (order == null) return NotFound();

            // Mükerrer değerlendirmeyi engelleme kontrolü
            var alreadyRated = await _context.Ratings.AnyAsync(r => r.OrderId == orderId);
            if (alreadyRated)
            {
                TempData["Error"] = "Bu sipariş için zaten değerlendirme yapılmış.";
                return RedirectToAction("Dashboard");
            }

            var rating = new Rating
            {
                OrderId = orderId,
                MenuScore = menuScore,
                CatererScore = catererScore,
                Service = service,
                Speed = speed,
                Comment = comment,
                CreatedAt = DateTime.Now
            };

            _context.Ratings.Add(rating);
    
            // Sistem loglama altyapısına ekleme
            _context.SystemLogs.Add(new SystemLog {
                ActionType = "Değerlendirme Yapıldı",
                Description = $"Müşteri, #{orderId} numaralı sipariş için puanlama ve yorum yaptı.",
                UserId = userId,
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Değerlendirmeniz başarıyla iletildi!";
    
            return RedirectToAction("Dashboard");
        }
        
        
    }
}