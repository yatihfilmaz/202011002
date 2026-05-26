using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Bunu ekledik
using System.ComponentModel.DataAnnotations.Schema; // Bunu ekledik
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation; // Üste ekle

namespace project.Models;

public partial class MenuItem
{
    [Key]
    public int ItemId { get; set; }

    public int CatererId { get; set; }

    [Required(ErrorMessage = "Yemek adı zorunludur.")]
    [Display(Name = "Yemek Adı")]
    public string Title { get; set; } = null!;

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }
    
    [Display(Name = "Kategori")]
    public int? CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    [ValidateNever]
    public virtual Category Category { get; set; }

    [Required(ErrorMessage = "Fiyat zorunludur.")]
    [Display(Name = "Fiyat (₺)")]
    [Column(TypeName = "decimal(15, 2)")] // Hassasiyet belirttik
    public decimal Price { get; set; }
    
    public virtual ICollection<ItemRating> ItemRatings { get; set; } = new List<ItemRating>();
    
    [Required(ErrorMessage = "Stok bilgisi zorunludur.")]
    [Display(Name = "Stok Miktarı (Porsiyon)")]
    [Range(0, 10000, ErrorMessage = "Stok 0'dan küçük olamaz.")]
    public int StockQuantity { get; set; }
    
    // Models/MenuItem.cs içine eklenecekler:
    public bool IsCombo { get; set; } = false;

// Many-to-Many ilişkisi için navigasyon özellikleri
    public virtual ICollection<MenuComboItem> ComboContents { get; set; } = new List<MenuComboItem>();
    public virtual ICollection<MenuComboItem> IncludedInCombos { get; set; } = new List<MenuComboItem>();

    [Display(Name = "Görsel URL")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Satışta mı?")]
    public bool? IsActive { get; set; }

    [ValidateNever]
    public virtual User Caterer { get; set; } = null!;

    [ValidateNever]
    public virtual ICollection<Customization> Customizations { get; set; } = new List<Customization>();
    

    [ValidateNever]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
