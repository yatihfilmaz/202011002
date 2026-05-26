using System.ComponentModel.DataAnnotations;

namespace project.Models
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; } // Hangi kullanıcının şifresini değiştirdiğimizi bilmek için

        [Required(ErrorMessage = "Yeni şifre boş bırakılamaz.")]
        [MinLength(6, ErrorMessage = "Şifreniz en az 6 karakter olmalıdır.")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Şifre tekrarı boş bırakılamaz.")]
        [Compare("NewPassword", ErrorMessage = "Şifreler birbiriyle eşleşmiyor.")]
        public string ConfirmPassword { get; set; }
    }
}