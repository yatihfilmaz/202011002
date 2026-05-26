using System.ComponentModel.DataAnnotations.Schema;

namespace project.Models
{
    public class MenuComboItem
    {
        // Üst Menü ID'si (Örn: Köfte Menü)
        public int ComboId { get; set; }
        
        [ForeignKey("ComboId")]
        public virtual MenuItem Combo { get; set; } = null!;

        // İçindeki Alt Ürün ID'si (Örn: Mevsim Salata)
        public int SubItemId { get; set; }
        
        [ForeignKey("SubItemId")]
        public virtual MenuItem SubItem { get; set; } = null!;
    }
}