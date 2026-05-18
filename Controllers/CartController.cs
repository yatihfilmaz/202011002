using Microsoft.AspNetCore.Mvc;
using project.Models; 
using project.Extensions; 
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using project.Services;
using iText.Html2pdf;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace project.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CartController : Controller
    {
        private readonly DenemeContext _context;
        private readonly IEmailService _emailService;
        
        public CartController(DenemeContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService; 
        }

        // 1. Sepetimi Görüntüle
        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            return View(cart);
        }

        // 2. Sepete Yemek Ekle (MODAL'DAN GELEN VERİLERLE)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int itemId, List<int> selectedOptionIds, int quantity, IFormCollection form)
        {
            // Yemeği bul
            var menuItem = await _context.MenuItems.FindAsync(itemId);
            if (menuItem == null || menuItem.StockQuantity <= 0)
            {
                return NotFound();
            }

            // Radio button'lardan (Tekli seçimlerden) gelen verileri listeye dahil et
            if (selectedOptionIds == null) selectedOptionIds = new List<int>();
            
            foreach (var key in form.Keys)
            {
                if (key.StartsWith("SelectedOptions_"))
                {
                    if (int.TryParse(form[key], out int optionId))
                    {
                        selectedOptionIds.Add(optionId);
                    }
                }
            }

            // Seçilen opsiyonların detaylarını veritabanından çek (Fiyat farkları ve isimleri için)
            var chosenOptions = await _context.Customizations
                .Where(c => selectedOptionIds.Contains(c.OptionId))
                .ToListAsync();

            // CartCustomization (Sepet Özelleştirmesi) listesini oluştur
            var cartOptions = chosenOptions.Select(c => new CartCustomization
            {
                OptionId = c.OptionId,
                Name = c.Name,
                PriceChange = c.PriceChange ?? 0
            }).ToList();

            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();

            // SEPETTE AYNI ÜRÜN VE *AYNI SEÇENEKLERLE* VAR MI KONTROLÜ
            var existingItem = cart.FirstOrDefault(c => 
                c.ItemId == itemId && 
                c.SelectedCustomizations.Select(x => x.OptionId).OrderBy(x => x)
                .SequenceEqual(cartOptions.Select(x => x.OptionId).OrderBy(x => x))
            );

            if (existingItem != null)
            {
                // Aynı seçeneklerle zaten varsa sadece miktarı artır
                if (existingItem.Quantity + quantity <= menuItem.StockQuantity)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    existingItem.Quantity = menuItem.StockQuantity; // Maksimum stoka eşitle
                }
            }
            else
            {
                // Yeni ürün veya farklı seçeneklerle ekleniyorsa yeni satır oluştur
                cart.Add(new CartItem
                {
                    ItemId = menuItem.ItemId,
                    Title = menuItem.Title,
                    Price = menuItem.Price,
                    Quantity = quantity > 0 ? quantity : 1,
                    ImageUrl = menuItem.ImageUrl ?? "/images/default-food.png",
                    SelectedCustomizations = cartOptions // SEÇENEKLERİ SEPETE EKLE
                });
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);
            TempData["Success"] = $"{menuItem.Title} başarıyla sepete eklendi!";
            
            return RedirectToAction("Index", "Home"); 
        }

        // 3. Sepetten Ürün Çıkar
        [HttpPost]
        public IActionResult RemoveFromCart(int itemId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            var item = cart.FirstOrDefault(c => c.ItemId == itemId);
            
            if (item != null)
            {
                cart.Remove(item);
                HttpContext.Session.SetObjectAsJson("Cart", cart);
            }

            return RedirectToAction("Index");
        }
        
        // Modal için Yemek Detaylarını Getir
        [HttpGet]
        public async Task<IActionResult> GetMealDetails(int id)
        {
            var menuItem = await _context.MenuItems
                .Include(m => m.Customizations)
                .FirstOrDefaultAsync(m => m.ItemId == id);

            if (menuItem == null)
            {
                return NotFound();
            }

            return PartialView("_MealOptionsPartial", menuItem);
        }

        // 4. Sepeti Tamamen Boşalt
        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }
        
        // 5. Checkout (Ödeme Ekranı)
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart");
            if (cart == null || !cart.Any())
            {
                TempData["Error"] = "Sepetiniz boş. Lütfen önce ürün ekleyin.";
                return RedirectToAction("Index");
            }

            return View(cart); 
        }

        // 6. Ödemeyi Tamamla ve Siparişi Veritabanına Yaz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckoutProcess()
        {
            var cart = HttpContext.Session.GetObjectFromJson<List<CartItem>>("Cart");
            if (cart == null || !cart.Any()) return RedirectToAction("Index");

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var firstCartItem = cart.First();
            var menuItem = await _context.MenuItems.FindAsync(firstCartItem.ItemId);
            int catererId = menuItem.CatererId; 

            // 1. Ana Sipariş (Order) Kaydı
            var newOrder = new Order
            {
                UserId = userId,
                CatererId = catererId,
                TotalAmount = cart.Sum(i => i.Total), // Özelleştirmeli dinamik fiyatların toplamı
                OrderDate = DateTime.Now,
                Status = "Hazırlanıyor" 
            };

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync(); 

            // 2. Sipariş Detayları (OrderItems) ve Seçeneklerin (OrderItemOptions) Kaydı
            foreach (var item in cart)
            {
                // Birim fiyatı hesapla (Taban fiyat + Seçili opsiyonların fiyat farkı)
                decimal unitPriceWithOptions = item.Price + item.SelectedCustomizations.Sum(c => c.PriceChange);

                var orderItem = new OrderItem
                {
                    OrderId = newOrder.OrderId,
                    ItemId = item.ItemId,
                    UnitPrice = unitPriceWithOptions, // Fişte doğru görünmesi için güncellenmiş fiyatı kaydet
                    Quantity = item.Quantity,
                };
                
                _context.OrderItems.Add(orderItem);
                await _context.SaveChangesAsync(); // OrderItemId'nin oluşması için kaydet

                // Seçilen opsiyonları veritabanına bağla
                if (item.SelectedCustomizations != null && item.SelectedCustomizations.Any())
                {
                    foreach (var opt in item.SelectedCustomizations)
                    {
                        var orderItemOption = new OrderItemOption
                        {
                            OrderItemId = orderItem.OrderItemId, // OrderItem tablonuzdaki PK ID ismi
                            OptionId = opt.OptionId
                        };
                        _context.OrderItemOptions.Add(orderItemOption);
                    }
                }

                // Stoktan düşme
                var dbItem = await _context.MenuItems.FindAsync(item.ItemId);
                if(dbItem != null) {
                    dbItem.StockQuantity -= item.Quantity;
                }
            }

            await _context.SaveChangesAsync(); 
            
            // --- E-POSTA VE PDF OLUŞTURMA SÜRECİ ---
            try
            {
                var customer = await _context.Users.FindAsync(userId);
                var caterer = await _context.Users.FindAsync(newOrder.CatererId);

                if (customer != null && !string.IsNullOrEmpty(customer.Email))
                {
                    string customerSubject = $"Siparişiniz Alındı! Sipariş No: #{newOrder.OrderId}";
                    string customerBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 5px;'>
                            <h2 style='color: #198754;'>Siparişiniz İçin Teşekkür Ederiz!</h2>
                            <p>Merhaba <strong>{customer.FullName}</strong>,</p>
                            <p>Siparişiniz başarıyla alınmıştır. Siparişinize ait <strong>E-Fiş</strong> ve <strong>Mesafeli Satış Sözleşmesi</strong> e-posta ekinde PDF olarak bilgilerinize sunulmuştur.</p>
                            <hr style='border: 0; border-top: 1px solid #eee;' />
                            <p><strong>Sipariş Numarası:</strong> #{newOrder.OrderId}</p>
                            <p><strong>Toplam Tutar:</strong> {newOrder.TotalAmount.ToString("C2")}</p>
                            <p>Keyifli alışverişler dileriz!</p>
                        </div>";

                    var emailAttachments = new List<(string FileName, byte[] Content)>();

                    var orderItems = await _context.OrderItems
                        .Include(oi => oi.Item) 
                        .Where(oi => oi.OrderId == newOrder.OrderId)
                        .ToListAsync();

                    string itemsHtml = "";
                    if (orderItems != null && orderItems.Any())
                    {
                        foreach (var item in orderItems)
                        {
                            decimal subTotal = item.Quantity * item.UnitPrice; 
                            itemsHtml += $@"
                            <tr>
                                <td style='padding: 10px; border-bottom: 1px solid #eee;'>
                                    <strong>{item.Item?.Title ?? "Yemek"}</strong> <br/>
                                    <small style='color: #666;'>Birim Fiyat: {item.UnitPrice.ToString("C2")} | Adet: {item.Quantity}</small>
                                </td>
                                <td style='text-align: right; padding: 10px; border-bottom: 1px solid #eee; vertical-align: middle;'>
                                    {subTotal.ToString("C2")}
                                </td>
                            </tr>";
                        }
                    }

                    string receiptHtml = $@"
                    <div style='font-family: Helvetica, sans-serif; padding: 20px; color: #333; line-height: 1.3;'>
                        <h1 style='text-align: center; color: #0d6efd; margin-bottom: 5px;'>SİPARİŞ FİŞİ</h1>
                        <p style='text-align: center; color: #888; margin-top: 0; font-size: 12px;'>Fatura Yerine Geçmez</p>
                        <hr style='border: 1px solid #ccc;'/>
                        <p><strong>Tedarikçi (Restoran):</strong> {caterer?.FullName}</p>
                        <p><strong>Müşteri:</strong> {customer.FullName}</p>
                        <p><strong>Tarih:</strong> {newOrder.OrderDate?.ToString("dd.MM.yyyy HH:mm")}</p>
                        <p><strong>Sipariş No:</strong> #{newOrder.OrderId}</p>
                        <br/>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <thead>
                                <tr style='background-color: #f8f9fa; border-bottom: 2px solid #333;'>
                                    <th style='text-align: left; padding: 10px;'>Açıklama</th>
                                    <th style='text-align: right; padding: 10px;'>Tutar</th>
                                </tr>
                            </thead>
                            <tbody>
                                {itemsHtml}
                            </tbody>
                        </table>
                        <h2 style='text-align: right; margin-top: 20px; color: #198754;'>Genel Toplam: {newOrder.TotalAmount.ToString("C2")}</h2>
                    </div>";

                    string agreementHtml = $@"
                    <div style='font-family: Helvetica, sans-serif; padding: 30px; line-height: 1.6; color: #333;'>
                        <h2 style='text-align: center; text-decoration: underline;'>MESAFELİ SATIŞ SÖZLEŞMESİ</h2>
                        <br/>
                        <p><strong>MADDE 1 - TARAFLAR</strong></p>
                        <p><strong>Satıcı (Tedarikçi):</strong> {caterer?.FullName} <br/>
                           <strong>Alıcı (Müşteri):</strong> {customer.FullName}</p>
                        <br/>
                        <p><strong>MADDE 2 - SÖZLEŞMENİN KONUSU</strong></p>
                        <p>İşbu sözleşmenin konusu, Alıcı'nın Satıcı'ya ait platform üzerinden elektronik ortamda siparişini yaptığı <strong>{newOrder.OrderDate?.ToString("dd.MM.yyyy")}</strong> tarihli ve <strong>#{newOrder.OrderId}</strong> numaralı siparişin satışı ile ilgili yasal hakların belirlenmesidir.</p>
                        <br/>
                        <p><strong>MADDE 3 - SİPARİŞ TUTARI</strong></p>
                        <p>Siparişin toplam vergiler dahil tutarı <strong>{newOrder.TotalAmount.ToString("C2")}</strong> olarak tahsil edilmiştir.</p>
                    </div>";

                    using (var receiptStream = new MemoryStream())
                    {
                        HtmlConverter.ConvertToPdf(receiptHtml, receiptStream);
                        emailAttachments.Add(($"Siparis_Fisi_{newOrder.OrderId}.pdf", receiptStream.ToArray()));
                    }

                    using (var agreementStream = new MemoryStream())
                    {
                        HtmlConverter.ConvertToPdf(agreementHtml, agreementStream);
                        emailAttachments.Add(($"Satis_Sozlesmesi_{newOrder.OrderId}.pdf", agreementStream.ToArray()));
                    }

                    await _emailService.SendEmailAsync(customer.Email, customerSubject, customerBody, emailAttachments);
                }

                if (caterer != null && !string.IsNullOrEmpty(caterer.Email))
                {
                    string catererSubject = $"🚨 Yeni Sipariş Geldi! Sipariş No: #{newOrder.OrderId}";
                    string catererBody = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ffc107; border-radius: 5px; background-color: #fffdf5;'>
                            <h2 style='color: #d63384;'>Yeni Sipariş Bildirimi</h2>
                            <p>Merhaba <strong>{caterer.FullName}</strong>,</p>
                            <p>Sisteminize <strong>#{newOrder.OrderId}</strong> numaralı yeni bir yemek siparişi düşmüştür.</p>
                            <hr style='border: 0; border-top: 1px solid #ffeeba;' />
                            <p><strong>Müşteri:</strong> {customer?.FullName}</p>
                            <p><strong>Sipariş Tutarı:</strong> {newOrder.TotalAmount.ToString("C2")}</p>
                            <p>Lütfen en kısa sürede panelinizce giriş yaparak siparişi hazırlamaya başlayın.</p>
                        </div>";

                    await _emailService.SendEmailAsync(caterer.Email, catererSubject, catererBody);
                }
            }
            catch (Exception)
            {
                // Hata mesajı yutulur
            }

            HttpContext.Session.Remove("Cart");

            TempData["Success"] = "Ödeme başarılı! Siparişiniz alındı.";
            
            return RedirectToAction("Success", new { orderId = newOrder.OrderId }); 
        }
        
        [HttpGet]
        // GET: /Cart/Success?orderId=5
        [HttpGet]
        public async Task<IActionResult> Success(int orderId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Siparişi, restoran (caterer) bilgilerini, yemekleri ve sipariş anında seçilen opsiyonları veritabanından çekiyoruz
            var order = await _context.Orders
                .Include(o => o.Caterer)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Item)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.OrderItemOptions)
                .ThenInclude(oio => oio.Option)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }
    }
}