using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace project.ViewModels
{
    public class ComboCreateViewModel
    {
        [Required(ErrorMessage = "Menü adı zorunludur.")]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required(ErrorMessage = "Fiyat zorunludur.")]
        public decimal Price { get; set; }

        public int StockQuantity { get; set; }

        public int? CategoryId { get; set; }

        // Menüye ait görsel
        public IFormFile? Image { get; set; }

        // Seçilen mevcut yemeklerin (SubItem) ID'lerini tutacak liste
        [Required(ErrorMessage = "Lütfen menüye dahil edilecek en az bir yemek seçin.")]
        public List<int> SelectedItemIds { get; set; } = new List<int>();
    }
}