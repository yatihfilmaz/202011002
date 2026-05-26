using System;
using System.Collections.Generic;

namespace project.Models;

public partial class Rating
{
    public int RatingId { get; set; }

    public int? OrderId { get; set; }

    public int MenuScore { get; set; }

    public int CatererScore { get; set; }

    public string? Comment { get; set; }

    public int Service { get; set; }

    public int Speed { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public virtual Order? Order { get; set; }
}
