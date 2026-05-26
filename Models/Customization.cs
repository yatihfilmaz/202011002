using System;
using System.Collections.Generic;

namespace project.Models;

public partial class Customization
{
    
    public int OptionId { get; set; }
    
    public bool IsSingleSelect { get; set; }

    public int? ItemId { get; set; }

    public string? OptionGroup { get; set; }

    public string Name { get; set; } = null!;

    public decimal? PriceChange { get; set; }
    
    public virtual MenuItem? Item { get; set; }

    public virtual ICollection<OrderItemOption> OrderItemOptions { get; set; } = new List<OrderItemOption>();
}
