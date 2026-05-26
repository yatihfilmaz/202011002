using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using project.Models; // Kendi namespace yolun
using Microsoft.AspNetCore.Mvc.Rendering;
using project.Services;
using iText.Html2pdf;
using System.IO;
using System.Collections.Generic;

namespace project.Controllers
{
    [Authorize(Roles = "Caterer")] // Sadece onaylı tedarikçiler girebilir
    public class CatererController : Controller
    {
        private readonly DenemeContext _context;
        private readonly IEmailService _emailService; // YENİ EKLENDİ

        public CatererController(DenemeContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }
        
        // 1. Yemek Listeleme
        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Include(m => m.Category) eklendi!
            var myMeals = await _context.MenuItems
                .Include(m => m.Category) 
                .Where(m => m.CatererId == userId)
                .ToListAsync();

            return View(myMeals);
        }

   
        // 2. Yeni Yemek Ekleme Sayfası (GET)
        public IActionResult Create()
        {
            // Kategorileri veritabanından çekip sayfaya (View'a) gönderiyoruz
            ViewBag.Categories = new SelectList(_context.Categories.ToList(), "CategoryId", "Name");
    
            return View();
        }

        // 3. Yeni Yemek Ekleme (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MenuItem menuItem)
        {
            if (ModelState.IsValid)
            {
                // Yemeği ekleyen tedarikçinin ID'sini otomatik atıyoruz
                menuItem.CatererId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                
                _context.Add(menuItem);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = "Yemek başarıyla menüye eklendi!";
                return RedirectToAction(nameof(Index));
            }
            return View(menuItem);
        }
        
        public async Task<IActionResult> Edit(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.ItemId == id && m.CatererId == userId);

            if (menuItem == null) return NotFound();
    
            // Düzenleme ekranı açıldığında, o yemeğin mevcut kategorisi seçili olarak gelsin
            ViewBag.Categories = new SelectList(_context.Categories.ToList(), "CategoryId", "Name", menuItem.CategoryId);
    
            return View(menuItem);
        }
        
        // 4. Caterer Dashboard (İstatistikler ve Gelen Siparişler)
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Bu tedarikçiye ait TÜM siparişleri çekiyoruz
            // Bu tedarikçiye ait TÜM siparişleri çekiyoruz
            var orders = await _context.Orders
                .Include(o => o.User)
                .Where(o => o.CatererId == userId)
                // Hem Tamamlandı hem de İptal Edildi statüsündekileri (true dönenleri) listenin sonuna iter
                .OrderBy(o => o.Status == "Tamamlandı" || o.Status == "İptal Edildi") 
                .ThenByDescending(o => o.OrderDate) 
                .ToListAsync();
            
            // İstatistikleri Hesapla ve ViewBag ile View'a Gönder
            ViewBag.TotalOrders = orders.Count;
            ViewBag.CompletedOrders = orders.Count(o => o.Status == "Tamamlandı");
    
            ViewBag.TotalRevenue = orders
                .Where(o => o.Status == "Tamamlandı")
                .Sum(o => o.TotalAmount);

            // Ayrıştırma yapmadan tüm listeyi doğrudan modele gönderiyoruz
            return View(orders);
        }

        // 5. Sipariş Durumunu Güncelleme (POST)
        [HttpPost]
public async Task<IActionResult> UpdateOrderStatus(int orderId, string newStatus)
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
    
    // YENİ: Restoran ismini PDF'te düzgün gösterebilmek için .Include(o => o.Caterer) ekledik
    var order = await _context.Orders
        .Include(o => o.Caterer)
        .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CatererId == userId);

    if (order != null)
    {
        order.Status = newStatus;

        // Loglama Yapısı
        var log = new SystemLog
        {
            ActionType = "Sipariş Güncelleme",
            Description = $"#{orderId} numaralı siparişin durumu '{newStatus}' yapıldı.",
            UserId = userId,
            Timestamp = DateTime.Now
        };
        _context.SystemLogs.Add(log);

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Sipariş #{order.OrderId} durumu '{newStatus}' olarak güncellendi.";

        // --- MÜŞTERİYE E-POSTA GÖNDERME VE PDF EKLEME ---
        var customer = await _context.Users.FindAsync(order.UserId);
        if (customer != null && !string.IsNullOrEmpty(customer.Email))
        {
            string subject = $"Siparişiniz Güncellendi: #{order.OrderId}";
            string body = $@"
                <div style='font-family: Arial; padding: 20px; border: 1px solid #ddd;'>
                    <h2 style='color: #198754;'>Sipariş Durumu Bilgilendirmesi</h2>
                    <p>Merhaba <strong>{customer.FullName}</strong>,</p>
                    <p><strong>#{order.OrderId}</strong> numaralı siparişinizin durumu an itibarıyla 
                    <span style='background-color: #20c997; color:white; padding: 4px 8px; font-weight: bold; border-radius:4px;'>{newStatus}</span> olarak güncellenmiştir.</p>
                    {(newStatus == "Tamamlandı" ? "<p>Siparişinize ait e-fatura/fiş ve mesafeli satış sözleşmesi e-posta ekinde yer almaktadır.</p>" : "")}
                    <p>Bizi tercih ettiğiniz için teşekkür ederiz!</p>
                </div>";

            List<(string FileName, byte[] Content)> emailAttachments = null;

            // Eğer sipariş başarıyla tamamlandıysa PDF'leri arka planda üretip liste içine atıyoruz
            if (newStatus == "Tamamlandı")
            {
                emailAttachments = new List<(string FileName, byte[] Content)>();

                // 1. Fiş HTML İçeriği (TotalAmount hatasız)
                string receiptHtml = $@"
                <div style='font-family: Helvetica, sans-serif; padding: 20px; color: #333;'>
                    <h1 style='text-align: center; color: #0d6efd;'>SİPARİŞ FİŞİ</h1>
                    <hr style='border: 1px solid #ccc;'/>
                    <p><strong>Tedarikçi (Restoran):</strong> {order.Caterer?.FullName}</p>
                    <p><strong>Müşteri:</strong> {customer.FullName}</p>
                    <p><strong>Tarih:</strong> {order.OrderDate?.ToString("dd.MM.yyyy HH:mm")}</p>
                    <p><strong>Sipariş No:</strong> #{order.OrderId}</p>
                    <br/>
                    <table style='width: 100%; border-collapse: collapse;'>
                        <tr style='background-color: #f8f9fa; border-bottom: 2px solid #333;'>
                            <th style='text-align: left; padding: 10px;'>Açıklama</th>
                            <th style='text-align: right; padding: 10px;'>Tutar</th>
                        </tr>
                        <tr>
                            <td style='padding: 10px; border-bottom: 1px solid #eee;'>Yemek Siparişi Ödemesi</td>
                            <td style='text-align: right; padding: 10px; border-bottom: 1px solid #eee;'>{order.TotalAmount.ToString("C2")}</td>
                        </tr>
                    </table>
                    <h2 style='text-align: right; margin-top: 20px;'>Genel Toplam: {order.TotalAmount.ToString("C2")}</h2>
                </div>";

                // 2. Sözleşme HTML İçeriği
                string agreementHtml = $@"
                <div style='font-family: Helvetica, sans-serif; padding: 30px; line-height: 1.6; color: #333;'>
                    <h2 style='text-align: center; text-decoration: underline;'>MESAFELİ SATIŞ SÖZLEŞMESİ</h2>
                    <br/>
                    <p><strong>MADDE 1 - TARAFLAR</strong></p>
                    <p><strong>Satıcı (Tedarikçi):</strong> {order.Caterer?.FullName} <br/>
                       <strong>Alıcı (Müşteri):</strong> {customer.FullName}</p>
                    <br/>
                    <p><strong>MADDE 2 - SÖZLEŞMENİN KONUSU</strong></p>
                    <p>İşbu sözleşmenin konusu, Alıcı'nın Satıcı'ya ait platform üzerinden elektronik ortamda siparişini yaptığı <strong>{order.OrderDate?.ToString("dd.MM.yyyy")}</strong> tarihli ve <strong>#{order.OrderId}</strong> numaralı siparişin satışı ile yasal hakların belirlenmesidir.</p>
                    <br/>
                    <p><strong>MADDE 3 - SİPARİŞ TUTARI</strong></p>
                    <p>Siparişin toplam vergiler dahil tutarı <strong>{order.TotalAmount.ToString("C2")}</strong> olarak tahsil edilmiştir.</p>
                </div>";

                // iText7 kullanarak HTML yapılarını byte dizisine çeviriyoruz
                using (var receiptStream = new MemoryStream())
                {
                    HtmlConverter.ConvertToPdf(receiptHtml, receiptStream);
                    emailAttachments.Add(($"Fis_{order.OrderId}.pdf", receiptStream.ToArray()));
                }

                using (var agreementStream = new MemoryStream())
                {
                    HtmlConverter.ConvertToPdf(agreementHtml, agreementStream);
                    emailAttachments.Add(($"Sozlesme_{order.OrderId}.pdf", agreementStream.ToArray()));
                }
            }

            // Maili ekleriyle birlikte gönderiyoruz (Eğer durum tamamlandı değilse emailAttachments null gider ve eksiz yollanır)
            await _emailService.SendEmailAsync(customer.Email, subject, body, emailAttachments);
        }
        // --------------------------------------------------
    }
    else
    {
        TempData["Error"] = "Sipariş güncellenirken bir hata oluştu.";
    }

    return RedirectToAction(nameof(Dashboard));
}

[HttpGet]
        public async Task<IActionResult> ManageCustomizations(int id) // id = ItemId
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            
            // Yemeği ve yemeğe ait mevcut seçenekleri veritabanından çekiyoruz
            var menuItem = await _context.MenuItems
                .Include(m => m.Customizations)
                .FirstOrDefaultAsync(m => m.ItemId == id && m.CatererId == userId);

            if (menuItem == null)
            {
                TempData["Error"] = "Yemek bulunamadı veya bu yemeği düzenleme yetkiniz yok.";
                return RedirectToAction("Index"); // Menü listesine geri dön
            }

            return View(menuItem); 
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> AddCustomization(int itemId, string optionGroup, string name, decimal priceChange)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
    
            // Güvenlik: Yemeğin bu tedarikçiye ait olduğundan emin olalım
            var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.ItemId == itemId && m.CatererId == userId);
    
            if (menuItem != null && !string.IsNullOrEmpty(name))
            {
                var newOption = new Customization
                {
                    ItemId = itemId,
                    // Trim() ekledik ki başındaki/sonundaki gereksiz boşluklar silinsin
                    OptionGroup = string.IsNullOrWhiteSpace(optionGroup) ? "Diğer Seçenekler" : optionGroup.Trim(),
                    Name = name.Trim(),
                    PriceChange = priceChange
                };
        
                _context.Customizations.Add(newOption);
                await _context.SaveChangesAsync();
        
                TempData["Success"] = $"'{name}' seçeneği başarıyla eklendi.";
            }
    
            return RedirectToAction("ManageCustomizations", new { id = itemId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCustomization(int optionId, int itemId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            
            // Güvenlik: Silinecek seçeneğin bağlı olduğu yemeğin sahibi bu tedarikçi mi?
            var option = await _context.Customizations
                .Include(c => c.Item)
                .FirstOrDefaultAsync(c => c.OptionId == optionId && c.Item.CatererId == userId);

            if (option != null)
            {
                _context.Customizations.Remove(option);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Seçenek başarıyla silindi.";
            }

            return RedirectToAction("ManageCustomizations", new { id = itemId });
        }
    }
}