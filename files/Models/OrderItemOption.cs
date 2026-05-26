using System;
using System.Collections.Generic;

namespace project.Models;

public partial class OrderItemOption
{
    public int RecordId { get; set; }

    public int? OrderItemId { get; set; }

    public int? OptionId { get; set; }

    public virtual Customization? Option { get; set; }

    public virtual OrderItem? OrderItem { get; set; }
}
