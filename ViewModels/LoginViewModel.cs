using System.ComponentModel.DataAnnotations;
namespace project.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "E-posta adresi zorunludur.")]
    [EmailAddress]
    public string Email { get; set; }

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [Display(Name = "Beni Hatırla")]
    public bool RememberMe { get; set; }
}