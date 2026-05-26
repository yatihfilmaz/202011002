using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using project.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace project.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly DenemeContext _context;
    
    public HomeController(ILogger<HomeController> logger, DenemeContext context)
    {
        _logger = logger;
        _context = context;
    }

    // Ana Sayfa - Restoranları (Caterer) Listeleme
    public async Task<IActionResult> Index()
    {
        // Sadece 'Caterer' rolünde olan ve onaylanmış kullanıcıları (restoranları) getir
        var caterers = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Role.RoleName == "Caterer" && u.IsApproved == true)
            .ToListAsync();

        decimal? userLat = null;
        decimal? userLng = null;

        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdStr))
            {
                int userId = int.Parse(userIdStr);
                var currentUser = await _context.Users.FindAsync(userId);
                if (currentUser != null)
                {
                    userLat = currentUser.Latitude;
                    userLng = currentUser.Longitude;
                }
            }
        }

        // Eğer kullanıcının DB'de konumu yoksa varsayılan olarak Ankara/Kızılay koordinatlarını atıyoruz.
        ViewBag.UserLat = userLat?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "39.920770";
        ViewBag.UserLng = userLng?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "32.854110";
        
        return View(caterers); // Artık View'e yemekleri değil, restoranları gönderiyoruz
    }

    // YENİ EKLENEN METOT: Restoranın kendi menü sayfası
    // YENİ EKLENEN METOT: Restoranın kendi menü sayfası
    public async Task<IActionResult> CatererMenu(int id, int? categoryId)
{
    var caterer = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
    if (caterer == null) return NotFound();

    ViewBag.CatererName = caterer.FullName;
    ViewBag.CatererId = id;

    var ratings = await _context.Ratings
        .Include(r => r.Order)
        .ThenInclude(o => o.User)
        .Include(r => r.Order)
        .ThenInclude(o => o.OrderItems)
        .ThenInclude(oi => oi.Item)
        .Where(r => r.Order.CatererId == id)
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync();

    ViewBag.TotalReviews = ratings.Count;
    if (ratings.Any())
    {
        ViewBag.AvgMenu = Math.Round(ratings.Average(r => r.MenuScore), 1);
        ViewBag.AvgCaterer = Math.Round(ratings.Average(r => r.CatererScore), 1);
        ViewBag.AvgService = Math.Round(ratings.Average(r => r.Service), 1);
        ViewBag.AvgSpeed = Math.Round(ratings.Average(r => r.Speed), 1);
    }
    else
    {
        ViewBag.AvgMenu = 0;
        ViewBag.AvgCaterer = 0;
        ViewBag.AvgService = 0;
        ViewBag.AvgSpeed = 0;
    }
    ViewBag.Reviews = ratings;

    // === GÜNCELLENEN KISIM BURASI ===
    var mealsQuery = _context.MenuItems
        .Include(m => m.Category)
        .Include(m => m.ItemRatings)
        .Include(m => m.Caterer)
        .Include(m => m.Customizations) // YENİ: Normal yemeğin özelleştirmeleri
        .Include(m => m.ComboContents) // YENİ: EĞER MENÜYSE ara tabloya git
            .ThenInclude(cc => cc.SubItem) // YENİ: Ara tablodan alt yemeğe (örn: Salata) git
                .ThenInclude(si => si.Customizations) // YENİ: Alt yemeğin çıkarılabilir malzemelerine git
        .Where(m => m.CatererId == id && m.IsActive == true && m.StockQuantity > 0);

    if (categoryId.HasValue)
    {
        mealsQuery = mealsQuery.Where(m => m.CategoryId == categoryId.Value);
    }

    var meals = await mealsQuery.ToListAsync();

    ViewBag.Categories = await _context.Categories.ToListAsync();
    ViewBag.SelectedCategoryId = categoryId; 

    return View(meals);
}

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}