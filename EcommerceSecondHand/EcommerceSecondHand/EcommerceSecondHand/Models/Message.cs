using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceSecondHand.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string SenderId { get; set; } = string.Empty;
        
        [Required]
        public string ReceiverId { get; set; } = string.Empty;
        
        [Required]
        [StringLength(1000)]
        public string Content { get; set; } = string.Empty;
        
        public bool IsRead { get; set; } = false;
        
        public DateTime DateSent { get; set; } = DateTime.UtcNow;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ReadAt { get; set; }
        
        // Property for MessagesController.cs - đồng bộ với DateSent
        public DateTime Timestamp 
        { 
            get { return DateSent; }
            set { DateSent = value; }
        }
        
        // Property alias for MessagesController.cs
        public string RecipientId 
        { 
            get { return ReceiverId; }
            set { ReceiverId = value; } 
        }
        
        // Property for Pages/Messages/Index.cshtml
        public DateTime SentAt 
        { 
            get { return DateSent; }
            set { DateSent = value; } 
        }
        
        // Navigation properties
        [ForeignKey("SenderId")]
        public ApplicationUser? Sender { get; set; }
        
        [ForeignKey("ReceiverId")]
        public ApplicationUser? Receiver { get; set; }
    }
}