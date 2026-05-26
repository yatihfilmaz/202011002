using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using project.Models;

namespace project.Controllers
{
    [Authorize(Roles = "Admin")] // Sadece Admin rolüne sahip olanlar girebilir
    public class AdminController : Controller
    {
        private readonly DenemeContext _context;

        public AdminController(DenemeContext context)
        {
            _context = context;
        }

        // 1. Dashboard (İstatistikler ve Son Aktiviteler)
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _context.Users.CountAsync(u => u.RoleId == 3);
            ViewBag.TotalCaterers = await _context.Users.CountAsync(u => u.RoleId == 2);
            ViewBag.PendingCaterers = await _context.Users.CountAsync(u => u.RoleId == 2 && u.IsApproved == false);
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.TotalRevenue = await _context.Orders.Where(o => o.Status == "Tamamlandı").SumAsync(o => o.TotalAmount);
            
            // Son 10 sistem logunu anasayfada göster
            var recentLogs = await _context.SystemLogs
                .Include(l => l.User)
                .OrderByDescending(l => l.Timestamp)
                .Take(10)
                .ToListAsync();
                
            return View(recentLogs);
        }
        
        // Değerlendirmeleri Listeleme Sayfası
        public async Task<IActionResult> Ratings()
        {
            // Rating -> Order -> User & Caterer ilişkisi kurularak veriler çekiliyor
            var ratings = await _context.Ratings
                .Include(r => r.Order)
                .ThenInclude(o => o.User)      // Siparişi veren müşteri
                .Include(r => r.Order)
                .ThenInclude(o => o.Caterer)   // Siparişi alan restoran
                .OrderByDescending(r => r.CreatedAt) // En yeni değerlendirmeler üstte
                .ToListAsync();

            return View(ratings);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRating(int id)
        {
            var rating = await _context.Ratings.FindAsync(id);
            if (rating != null)
            {
                _context.Ratings.Remove(rating);
        
                // ADMİN İŞLEMİ LOGU
                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                _context.SystemLogs.Add(new SystemLog {
                    ActionType = "Değerlendirme Silindi",
                    Description = $"Admin, RatingID'si {id} olan müşteri değerlendirmesini sildi.",
                    UserId = adminId,
                    Timestamp = DateTime.Now
                });
        
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Ratings));
        }

        // 2. Sistem Logları (Erişim ve İşlem Kayıtları)
        // 2. Sistem Logları (Erişim ve İşlem Kayıtları)
        [HttpGet]
        public async Task<IActionResult> Logs(string actionType)
        {
            // 1. Filtre menüsü (Dropdown) için benzersiz log türlerini veritabanından çekiyoruz
            ViewBag.ActionTypes = await _context.SystemLogs
                .Select(l => l.ActionType)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
        
            // Seçili olan filtreyi View'da tutabilmek için ViewBag'e atıyoruz
            ViewBag.SelectedActionType = actionType;

            // 2. Log sorgusunu başlatıyoruz
            var query = _context.SystemLogs
                .Include(l => l.User)
                .AsQueryable();

            // 3. Eğer kullanıcı dropdown'dan bir tür seçmişse, sorguyu ona göre filtreliyoruz
            if (!string.IsNullOrEmpty(actionType))
            {
                query = query.Where(l => l.ActionType == actionType);
            }

            // 4. Tarihe göre azalan şekilde sıralayıp listeye çeviriyoruz
            var logs = await query.OrderByDescending(l => l.Timestamp).ToListAsync();
    
            return View(logs);
        }

        // 3. Müşterileri Listeleme ve Yönetme
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users.Where(u => u.RoleId == 3).ToListAsync();
            return View(users);
        }

        // 4. Tedarikçileri (Restoranları) Listeleme ve Yönetme
        public async Task<IActionResult> Caterers()
        {
            var caterers = await _context.Users.Where(u => u.RoleId == 2).ToListAsync();
            return View(caterers);
        }
        
        // 5. Siparişleri İnceleme (Tüm Sistemdeki)
        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Caterer)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View(orders);
        }

        // --- VERİ YÖNETİM METOTLARI (Data Management) ---
        
        // Tedarikçi Başvurusunu Onaylama
        [HttpPost]
        public async Task<IActionResult> ApproveCaterer(int id)
        {
            var caterer = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id && u.RoleId == 2);
            if (caterer != null)
            {
                caterer.IsApproved = true; // Onaylandı olarak işaretle
                
                // İşlemi Logla
                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                _context.SystemLogs.Add(new SystemLog {
                    ActionType = "Tedarikçi Onayı",
                    Description = $"Admin, '{caterer.FullName}' adlı tedarikçinin başvurusunu onayladı.",
                    UserId = adminId,
                    Timestamp = DateTime.Now
                });
                
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{caterer.FullName} başarıyla onaylandı ve sisteme dahil edildi.";
            }
            return RedirectToAction("Caterers");
        }

        // Kullanıcı veya Tedarikçiyi Engelleme / Engel Kaldırma (Ban / Unban)
        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                user.IsApproved = !user.IsApproved; // Durumu tersine çevir
                
                // İşlemi Logla
                var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                _context.SystemLogs.Add(new SystemLog {
                    ActionType = user.IsApproved ? "Hesap Aktifleştirildi" : "Hesap Engellendi",
                    Description = $"Admin, '{user.FullName}' (ID: {user.UserId}) hesabının durumunu değiştirdi.",
                    UserId = adminId,
                    Timestamp = DateTime.Now
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Hesap durumu başarıyla güncellendi.";
            }
            return Redirect(Request.Headers["Referer"].ToString()); // İşlem yapıldığı sayfaya geri dön
        }
        
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Caterer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Item)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemOptions)
                .ThenInclude(oio => oio.Option)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();

            // Detayları modal içinde gösterebilmek için partial view dönüyoruz
            return PartialView("_OrderDetailsPartial", order);
        }
    }
}