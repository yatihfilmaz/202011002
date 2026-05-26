using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using project.Models; // Kendi proje adına göre düzelt
using project.ViewModels; // Kendi proje adına göre düzelt
using project.Services;
using Microsoft.EntityFrameworkCore;


namespace CateringWebProject.Controllers
{
    public class AuthController : Controller
    {
        private readonly DenemeContext _context;
        private readonly IEmailService _emailService;


        public AuthController(DenemeContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // --- 1. KAYIT OL (REGISTER) ---
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Lütfen e-posta adresinizi giriniz.");
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                // Güvenlik gereği "Böyle bir kullanıcı yok" demek yerine genel bir mesaj verilir.
                TempData["Success"] = "Eğer sistemimizde bu e-posta adresiyle kayıtlı bir hesabınız varsa, şifre sıfırlama bağlantısı gönderilmiştir.";
                return RedirectToAction("Login");
            }

            // Güvenli Token Üretimi ve 1 Saatlik Ömür Biçme
            string resetToken = Guid.NewGuid().ToString();
            user.ResetPasswordToken = resetToken;
            user.ResetTokenExpires = DateTime.Now.AddHours(1);

            // 1. EKLENEN LOG: Şifre sıfırlama talebini admin paneline düşürür
            _context.SystemLogs.Add(new SystemLog {
                ActionType = "Şifre Sıfırlama Talebi",
                Description = $"'{user.Email}' kullanıcısı şifre sıfırlama bağlantısı talep etti.",
                UserId = user.UserId,
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();

            // Şifre Sıfırlama Mailini Gönderme
            try
            {
                var resetUrl = Url.Action("ResetPassword", "Auth", new { token = resetToken }, Request.Scheme);

                string subject = "Şifre Sıfırlama Talebi";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                        <h2 style='color: #dc3545;'>Şifre Sıfırlama</h2>
                        <p>Merhaba <strong>{user.FullName}</strong>,</p>
                        <p>Hesabınız için şifre sıfırlama talebinde bulunulmuştur. Yeni şifrenizi belirlemek için aşağıdaki butona tıklayın:</p>
                        <br/>
                        <a href='{resetUrl}' style='background-color: #0d6efd; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Şifremi Yenile</a>
                        <br/><br/>
                        <p style='color: #888; font-size: 12px;'>Bu bağlantının süresi 1 saat sonra dolacaktır. Eğer bu talebi siz yapmadıysanız, bu e-postayı dikkate almayınız ve şifrenizi güvende tutunuz.</p>
                    </div>";

                await _emailService.SendEmailAsync(user.Email, subject, body);
            }
            catch (Exception)
            {
                // Loglama yapılabilir
            }

            TempData["Success"] = "Şifre sıfırlama bağlantısı e-posta adresinize gönderildi.";
            return RedirectToAction("Login");
        }

        // =======================================================
        // 2. YENİ ŞİFRE BELİRLEME EKRANI
        // =======================================================
        

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetPasswordToken == model.Token);

            if (user == null || user.ResetTokenExpires < DateTime.Now)
            {
                TempData["Error"] = "İşlem süresi dolmuş. Lütfen baştan başlayın.";
                return RedirectToAction("ForgotPassword");
            }

            // Yeni şifreyi BCrypt ile şifrele ve kaydet
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            // Kullanılan token'ı bir daha kullanılamaması için çöpe atıyoruz
            user.ResetPasswordToken = null;
            user.ResetTokenExpires = null;

            // 2. GÜNCELLENEN LOG: Admin panelinde yeşil renkli "Başarılı" badge'i ile gözükmesini sağlar
            _context.SystemLogs.Add(new SystemLog
            {
                ActionType = "Başarılı Şifre Güncelleme",
                Description = $"'{user.Email}' kullanıcısı hesabının şifresini başarıyla yeniledi.",
                UserId = user.UserId,
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Şifreniz başarıyla güncellendi! Artık yeni şifrenizle giriş yapabilirsiniz.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Önce email kullanımda mı kontrolü (Sizde zaten vardır)
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("", "Bu e-posta adresi zaten kullanımda.");
                    return View(model);
                }

                decimal? lat = null;
                decimal? lng = null;
                if (!string.IsNullOrEmpty(model.Latitude) && !string.IsNullOrEmpty(model.Longitude))
                {
                    if (decimal.TryParse(model.Latitude, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal pLat)) lat = pLat;
                    if (decimal.TryParse(model.Longitude, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal pLng)) lng = pLng;
                }

                // 1. Benzersiz bir doğrulama kodu (Token) oluşturuyoruz
                string token = Guid.NewGuid().ToString();

                var newUser = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,

                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    RoleId = 3,

                    IsEmailVerified = false,
                    VerificationToken = token,
                    IsApproved = true,
                    Latitude = lat,
                    Longitude = lng
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync(); // Kullanıcı ID'sinin oluşması için kaydediyoruz

                // 2. Doğrulama Linkini Oluşturma ve E-Posta Gönderme
                try
                {
                    // Kullanıcının tıklayacağı linki otomatik olarak uygulamanın domainiyle oluşturur
                    var verificationUrl = Url.Action("VerifyEmail", "Auth", new { token = token }, Request.Scheme);

                    string subject = "Lütfen E-Posta Adresinizi Doğrulayın";
                    string body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 5px; text-align: center;'>
                    <h2 style='color: #0d6efd;'>Aramıza Hoş Geldiniz!</h2>
                    <p>Merhaba <strong>{newUser.FullName}</strong>,</p>
                    <p>Hesabınızı aktifleştirmek ve giriş yapabilmek için lütfen aşağıdaki butona tıklayın:</p>
                    <br/>
                    <a href='{verificationUrl}' style='background-color: #198754; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Hesabımı Doğrula</a>
                    <br/><br/>
                    <p style='color: #888; font-size: 12px;'>Eğer bu işlemi siz yapmadıysanız bu e-postayı dikkate almayınız.</p>
                </div>";

                    await _emailService.SendEmailAsync(newUser.Email, subject, body);
                }
                catch (Exception)
                {
                    // Mail gönderimi başarısız olsa bile kullanıcı kayıt olmuş olur, daha sonra tekrar mail istetebilir.
                }

                TempData["Success"] = "Kayıt başarılı! Lütfen e-posta kutunuza giderek hesabınızı doğrulayın.";
                return RedirectToAction("Login");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult CatererApply()
        {
            return View(); // Views/Auth/CatererApply.cshtml sayfasını açacak
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> CatererApply(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // E-posta kontrolü
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Bu e-posta adresi zaten kullanılıyor.");
                    return View(model);
                }

                // KONUM VERİSİNİ ÇEVİRME
                decimal? lat = null;
                decimal? lng = null;
                if (!string.IsNullOrEmpty(model.Latitude) && !string.IsNullOrEmpty(model.Longitude))
                {
                    if (decimal.TryParse(model.Latitude, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal pLat)) lat = pLat;
                    if (decimal.TryParse(model.Longitude, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal pLng)) lng = pLng;
                }

                // Token oluşturma (Doğrulama linki için)
                string token = Guid.NewGuid().ToString();
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

                var newUser = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    PasswordHash = hashedPassword,
                    RoleId = 2, // Caterer rolü
                    IsApproved = false, // ADMIN ONAYI BEKLEYECEK
                    CreatedAt = DateTime.Now,
                    Latitude = lat,
                    Longitude = lng,

                    // YENİ EKLENEN KISIM
                    IsEmailVerified = false,
                    VerificationToken = token
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync(); // Kaydedip UserID oluşturuyoruz

                // YENİ EKLENEN MAİL GÖNDERME İŞLEMİ
                try
                {
                    var verificationUrl = Url.Action("VerifyEmail", "Auth", new { token = token }, Request.Scheme);

                    string subject = "Tedarikçi Başvurusu - Lütfen E-Posta Adresinizi Doğrulayın";
                    string body = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 5px; text-align: center;'>
                    <h2 style='color: #ffc107;'>Tedarikçi Başvurunuz Alındı!</h2>
                    <p>Merhaba <strong>{newUser.FullName}</strong>,</p>
                    <p>Restoran/Tedarikçi başvurunuz başarıyla sistemimize ulaşmıştır. İşlemlerinize devam edebilmek ve yönetici onay sürecini başlatmak için lütfen önce e-posta adresinizi doğrulayın:</p>
                    <br/>
                    <a href='{verificationUrl}' style='background-color: #198754; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold;'>E-Postamı Doğrula</a>
                    <br/><br/>
                    <p style='color: #888; font-size: 12px;'>E-posta adresinizi doğruladıktan sonra yöneticilerimiz başvurunuzu inceleyecek ve onaylandığında giriş yapabileceksiniz.</p>
                </div>";

                    await _emailService.SendEmailAsync(newUser.Email, subject, body);
                }
                catch (Exception)
                {
                    // Mail gönderilemezse loglanabilir
                }

                TempData["SuccessMessage"] =
                    "Başvurunuz alındı! Lütfen e-posta adresinize gönderilen linke tıklayarak hesabınızı doğrulayın. (Ardından yönetici onayı beklenecektir)";
                return RedirectToAction("Login");
            }

            return View(model);
        }


        // --- 2. GİRİŞ YAP (LOGIN) ---
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginViewModel model)
{
    if (ModelState.IsValid)
    {
        // Kullanıcıyı veritabanında E-posta adresine göre bul
        var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

        // Kullanıcı var mı ve şifre BCrypt ile uyuşuyor mu?
        if (user != null && BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            // E-Posta doğrulama kontrolü
            if (!user.IsEmailVerified)
            {
                _context.SystemLogs.Add(new SystemLog {
                    ActionType = "Giriş Engellendi",
                    Description = $"'{user.Email}' e-posta onayı olmadığı için girişi reddedildi.",
                    UserId = user.UserId,
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                ModelState.AddModelError("", "Giriş yapabilmek için lütfen önce e-posta adresinize gönderilen linkten hesabınızı doğrulayın.");
                return View(model);
            }

            // Tedarikçi onay kontrolü
            if (user.RoleId == 2 && !user.IsApproved)
            {
                _context.SystemLogs.Add(new SystemLog {
                    ActionType = "Giriş Engellendi",
                    Description = $"Tedarikçi '{user.Email}', yönetici onayı beklediği için girişi reddedildi.",
                    UserId = user.UserId,
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                ModelState.AddModelError(string.Empty, "Tedarikçi başvurunuz yönetici onayındadır. Onaylandıktan sonra giriş yapabilirsiniz.");
                return View(model);
            }

            // Sisteme tanıtılacak kimlik kartı (Claims) hazırlanıyor
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.RoleId == 1 ? "Admin" : (user.RoleId == 2 ? "Caterer" : "Customer"))
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe 
            };

            // Sisteme giriş yap
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // SADECE BAŞARILI İSE LOGLA
            _context.SystemLogs.Add(new SystemLog {
                ActionType = "Başarılı Giriş",
                Description = $"'{user.Email}' sisteme başarıyla giriş yaptı.",
                UserId = user.UserId,
                Timestamp = DateTime.Now
            });
            await _context.SaveChangesAsync();

            // Rolüne göre yönlendir
            if (user.RoleId == 1) return RedirectToAction("Index", "Admin");
            if (user.RoleId == 2) return RedirectToAction("Dashboard", "Caterer"); 

            return RedirectToAction("Index", "Home"); 
        }

        // KULLANICI YOK VEYA ŞİFRE YANLIŞSA SADECE HATALI GİRİŞ LOGU AT
        _context.SystemLogs.Add(new SystemLog {
            ActionType = "Hatalı Giriş",
            Description = $"'{model.Email}' adresi için yanlış şifre veya hatalı bilgi girildi.",
            UserId = user?.UserId,
            Timestamp = DateTime.Now
        });
        await _context.SaveChangesAsync();

        ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
    }

    return View(model);
}

        [HttpGet]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Geçersiz doğrulama bağlantısı.";
                return RedirectToAction("Index", "Home");
            }

            // Veritabanında bu token'a sahip bir kullanıcı arıyoruz
            var user = await _context.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);

            if (user == null)
            {
                TempData["Error"] = "Geçersiz veya süresi dolmuş bir doğrulama bağlantısı tıkladınız.";
                return RedirectToAction("Login");
            }

            // Kullanıcı bulunduysa hesabını onaylıyoruz
            user.IsEmailVerified = true;
            user.VerificationToken = null; // Token'ı tek kullanımlık yapmak için siliyoruz

            await _context.SaveChangesAsync();

            TempData["Success"] = "E-posta adresiniz başarıyla doğrulandı! Artık giriş yapabilirsiniz.";
            return RedirectToAction("Login");
        }

        // --- 3. ÇIKIŞ YAP (LOGOUT) ---
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // Çerezleri temizle ve oturumu kapat
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }
    }
}