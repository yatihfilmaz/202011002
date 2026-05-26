using System;
using System.ComponentModel.DataAnnotations;

namespace project.Models;

public class ChatMessage
{
    [Key]
    public int MessageId { get; set; }
    
    public int OrderId { get; set; }
    public virtual Order? Order { get; set; }

    public int SenderId { get; set; } // Mesajı gönderenin UserID'si
    public virtual User? Sender { get; set; }

    public string MessageText { get; set; } = null!;
    
    public DateTime SentAt { get; set; } = DateTime.Now;
}