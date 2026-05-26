using System.Collections.Generic;
using System.Linq;

namespace project.Models
{
    public class CartItem
    {
        public int ItemId { get; set; }
        public string Title { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = null!;

        // Seçilen özelleştirmeleri (sos, malzeme vb.) sepet satırında saklamak için eklenen liste
        public List<CartCustomization> SelectedCustomizations { get; set; } = new List<CartCustomization>();

        // Dinamik Fiyat Hesaplama Özelliği: 
        // (Yemek Taban Fiyatı + Seçilen Ekstraların Fiyat Farkları) * Adet
        public decimal Total => (Price + (SelectedCustomizations?.Sum(c => c.PriceChange) ?? 0)) * Quantity;
    }

    // Sepetteki her bir seçeneğin detayını tutacak yardımcı sınıf
    public class CartCustomization
    {
        public int OptionId { get; set; }
        public string Name { get; set; } = null!;
        public decimal PriceChange { get; set; }
    }
}