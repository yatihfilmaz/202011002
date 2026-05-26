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
using project.ViewModels;

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
                .Include(m => m.ItemRatings)
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
        
        [HttpGet]
public async Task<IActionResult> Profile()
{
    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
    int userId = int.Parse(userIdStr);
    
    var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
    if (user == null) return NotFound();

    return View(user);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Profile(string fullName, string email, string? newPassword, string? latStr, string? lngStr)
{
    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
    int userId = int.Parse(userIdStr);
    var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

    if (user == null) return NotFound();

    // Temel Bilgileri Güncelle
    user.FullName = fullName;
    user.Email = email;

    // Şifre alanı boş bırakılmadıysa, yeni şifreyi Hash'leyerek kaydet
    if (!string.IsNullOrWhiteSpace(newPassword))
    {
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
    }

    // Koordinatları güvenli bir şekilde kaydet (Nokta veya virgül fark etmeksizin)
    if (!string.IsNullOrWhiteSpace(latStr) && decimal.TryParse(latStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal lat))
    {
        user.Latitude = lat;
    }
    if (!string.IsNullOrWhiteSpace(lngStr) && decimal.TryParse(lngStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal lng))
    {
        user.Longitude = lng;
    }

    // Admin paneli için Log kaydı
    _context.SystemLogs.Add(new SystemLog
    {
        ActionType = "Profil Güncelleme",
        Description = $"'{user.Email}' hesabının profil bilgileri güncellendi.",
        UserId = user.UserId,
        Timestamp = DateTime.Now
    });

    await _context.SaveChangesAsync();

    TempData["Success"] = "Profil bilgileriniz başarıyla güncellendi.";
    return RedirectToAction("Profile");
}
        
        // --- YEMEĞİ DÜZENLEME EKRANINI AÇAN METOT ---
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int catererId = int.Parse(userIdStr);

            // Yemeği ve içindeki kombo kayıtlarını (menü ise) getir
            var menuItem = await _context.MenuItems
                .Include(m => m.ComboContents)
                .FirstOrDefaultAsync(m => m.ItemId == id && m.CatererId == catererId);

            if (menuItem == null) return NotFound();

            ViewBag.Categories = await _context.Categories.ToListAsync();
    
            // Restoranın menüye ekleyebileceği DİĞER TEKİL yemekleri listeliyoruz
            ViewBag.AllSingleItems = await _context.MenuItems
                .Where(m => m.CatererId == catererId && m.IsCombo == false && m.ItemId != id)
                .ToListAsync();

            return View(menuItem);
        }
        
        [HttpGet]
public async Task<IActionResult> CreateCombo()
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
    
    ViewBag.Categories = await _context.Categories.ToListAsync();
    
    // Sadece bu tedarikçiye ait, aktif olan ve kendisi zaten combo olmayan TEKİL yemekleri çekiyoruz.
    // .Include(m => m.Customizations) ile yemeklerin alt seçeneklerini de View'a taşıyoruz.
    ViewBag.MenuItems = await _context.MenuItems
        .Include(m => m.Customizations) 
        .Where(m => m.CatererId == userId && m.IsActive == true && m.IsCombo == false)
        .ToListAsync();

    return View();
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateCombo(ComboCreateViewModel model)
{
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

    if (ModelState.IsValid && model.SelectedItemIds != null && model.SelectedItemIds.Any())
    {
        // 1. Ana Combo Menüyü MenuItems tablosuna ekle
        var newCombo = new MenuItem
        {
            CatererId = userId,
            Title = model.Title,
            Description = model.Description,
            Price = model.Price,
            StockQuantity = model.StockQuantity,
            CategoryId = model.CategoryId,
            IsCombo = true,  // Bunun bir birleşik menü olduğunu işaretliyoruz
            IsActive = true
            // ImageUrl = ... (Görsel yükleme kodunuz varsa buraya entegre edebilirsiniz)
        };

        _context.MenuItems.Add(newCombo);
        await _context.SaveChangesAsync(); // ID'nin oluşması için DB'ye kaydediyoruz

        // 2. Seçilen yemekleri MenuComboItems tablosuna bağla
        foreach (var subItemId in model.SelectedItemIds)
        {
            var comboItem = new MenuComboItem
            {
                ComboId = newCombo.ItemId, // Yeni oluşan menünün ID'si
                SubItemId = subItemId      // Seçilen yemeğin ID'si
            };
            _context.MenuComboItems.Add(comboItem);
        }
        await _context.SaveChangesAsync();

        TempData["Success"] = "Kombinasyon menüsü başarıyla oluşturuldu!";
        return RedirectToAction("Index"); // Menü listesine dön
    }

    // Hata durumunda listeleri tekrar doldur
    ViewBag.Categories = await _context.Categories.ToListAsync();
    ViewBag.MenuItems = await _context.MenuItems
        .Include(m => m.Customizations)
        .Where(m => m.CatererId == userId && m.IsActive == true && m.IsCombo == false)
        .ToListAsync();

    TempData["Error"] = "Lütfen tüm zorunlu alanları doldurun ve en az bir yemek seçin.";
    return View(model);
}

// --- DÜZENLENEN BİLGİLERİ VERİTABANINA KAYDEDEN METOT ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, project.Models.MenuItem model, int[] selectedSubItemIds)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int catererId = int.Parse(userIdStr);

            if (id != model.ItemId) return BadRequest();

            var existingItem = await _context.MenuItems
                .Include(m => m.ComboContents)
                .FirstOrDefaultAsync(m => m.ItemId == id && m.CatererId == catererId);

            if (existingItem == null) return NotFound("Yemek bulunamadı.");

            if (ModelState.IsValid)
            {
                existingItem.Title = model.Title;
                existingItem.Description = model.Description;
                existingItem.Price = model.Price;
                existingItem.StockQuantity = model.StockQuantity;
                existingItem.CategoryId = model.CategoryId;
                existingItem.IsActive = model.IsActive;
                existingItem.ImageUrl = model.ImageUrl;
                existingItem.IsCombo = model.IsCombo; // Menü bayrağı güncellendi

                // Ara tablodaki eski menü içeriklerini temizle
                _context.MenuComboItems.RemoveRange(existingItem.ComboContents);
        
                // Eğer bu bir menüyse ve alt yemek seçilmişse bunları ara tabloya kaydet
                if (model.IsCombo && selectedSubItemIds != null)
                {
                    foreach (var subId in selectedSubItemIds)
                    {
                        _context.MenuComboItems.Add(new project.Models.MenuComboItem 
                        { 
                            ComboId = id, 
                            SubItemId = subId 
                        });
                    }
                }

                _context.Update(existingItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View(model);
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
    
    // Müşteri bilgisini alabilmek için .Include(o => o.User) ekledik
    var order = await _context.Orders
        .Include(o => o.Caterer)
        .Include(o => o.User)
        .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CatererId == userId);

    if (order != null)
    {
        order.Status = newStatus;

        // Loglama
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

        // --- MÜŞTERİYE BİLGİLENDİRME E-POSTASI ---
        // Sadece bilgilendirme metni gönderilir, PDF ekleri (attachments) tamamen kaldırıldı.
        if (order.User != null && !string.IsNullOrEmpty(order.User.Email))
        {
            string subject = $"Siparişiniz Güncellendi: #{order.OrderId}";
            string body = $@"
                <div style='font-family: Arial; padding: 20px; border: 1px solid #ddd;'>
                    <h2 style='color: #198754;'>Sipariş Durumu Bilgilendirmesi</h2>
                    <p>Merhaba <strong>{order.User.FullName}</strong>,</p>
                    <p><strong>#{order.OrderId}</strong> numaralı siparişinizin durumu an itibarıyla 
                    <span style='background-color: #20c997; color:white; padding: 4px 8px; font-weight: bold; border-radius:4px;'>{newStatus}</span> olarak güncellenmiştir.</p>
                    <p>Bizi tercih ettiğiniz için teşekkür ederiz!</p>
                </div>";

            // Sadece body gönderiyoruz, üçüncü parametre (attachment) yok
            await _emailService.SendEmailAsync(order.User.Email, subject, body);
        }
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
        
        [HttpPost]
        public async Task<IActionResult> AddCategoryAjax(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                return Json(new { success = false, message = "Kategori adı boş olamaz." });

            // Eğer aynı isimde bir kategori veritabanında zaten varsa direkt onun ID'sini dönüyoruz
            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.ToLower().Trim());

            if (existingCategory != null)
                return Json(new { success = true, id = existingCategory.CategoryId, name = existingCategory.Name });

            // Yoksa yeni oluşturup kaydediyoruz
            var newCategory = new Category { Name = categoryName.Trim() };
            _context.Categories.Add(newCategory);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = newCategory.CategoryId, name = newCategory.Name });
        }
        
        [HttpPost]
        public async Task<IActionResult> DeleteCategoryAjax(int categoryId)
        {
            var category = await _context.Categories.FindAsync(categoryId);
            if (category == null)
                return Json(new { success = false, message = "Kategori bulunamadı veya zaten silinmiş." });

            // GÜVENLİK: Eğer bu kategoriye ait sistemde kayıtlı yemek varsa silinmesini engelliyoruz
            bool isInUse = await _context.MenuItems.AnyAsync(m => m.CategoryId == categoryId);
            if (isInUse)
                return Json(new { success = false, message = "Bu kategoriye bağlı yemekler var. Silmek için önce o yemeklerin kategorisini değiştirmelisiniz." });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
    
    
}