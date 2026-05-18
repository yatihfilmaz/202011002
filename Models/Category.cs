using System.ComponentModel.DataAnnotations;

namespace project.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        public string Name { get; set; }

        // Bu kategorideki yemekleri tutacak liste (İleride filtreleme için işe yarayacak)
        public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}