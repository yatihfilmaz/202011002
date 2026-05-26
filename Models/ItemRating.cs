using System;
using System.ComponentModel.DataAnnotations; // Bu kütüphane [Key] için gerekli

namespace project.Models
{
    public partial class ItemRating
    {
        [Key] // EF Core'a bunun Primary Key olduğunu söylüyoruz
        public int RatingId { get; set; }
        
        public int ItemId { get; set; }
        public int UserId { get; set; }
        public int OrderId { get; set; }
        public int Score { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual MenuItem Item { get; set; }
        public virtual User User { get; set; }
        public virtual Order Order { get; set; }
    }
}