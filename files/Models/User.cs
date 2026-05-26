using System;
using System.Collections.Generic;

namespace project.Models;

public partial class User
{
    public int UserId { get; set; }

    public int? RoleId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }
    
    public bool IsEmailVerified { get; set; } = false;
    
    public string? VerificationToken { get; set; }

    public bool? Is2Faenabled { get; set; }

    public DateTime? CreatedAt { get; set; }
    
    public string? ResetPasswordToken { get; set; }
    
    public DateTime? ResetTokenExpires { get; set; }
    
    public bool IsApproved { get; set; }

    public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

    public virtual ICollection<Order> OrderCaterers { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderUsers { get; set; } = new List<Order>();

    public virtual Role? Role { get; set; }

    public virtual ICollection<SystemLog> SystemLogs { get; set; } = new List<SystemLog>();
}
