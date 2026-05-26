using System;
using System.Collections.Generic;

namespace project.Models;

public partial class OrderItem
{
    public int OrderItemId { get; set; }

    public int? OrderId { get; set; }

    public int? ItemId { get; set; }
    
    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual MenuItem? Item { get; set; }

    public virtual Order? Order { get; set; }

    public virtual ICollection<OrderItemOption> OrderItemOptions { get; set; } = new List<OrderItemOption>();
}
