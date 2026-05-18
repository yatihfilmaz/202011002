using System;
using System.Collections.Generic;

namespace project.Models;

public partial class SystemLog
{
    public int LogId { get; set; }

    public int? UserId { get; set; }

    public string ActionType { get; set; } = null!;

    public string Description { get; set; } = null!;

    public DateTime? Timestamp { get; set; }

    public virtual User? User { get; set; }
}
