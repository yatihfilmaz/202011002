using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace project.Models
{
    public class ChatMessage
    {
        [Key]
        public int MessageId { get; set; }
        
        public int OrderId { get; set; }
        public int SenderId { get; set; }
        
        public string MessageText { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;

        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }
    }
}