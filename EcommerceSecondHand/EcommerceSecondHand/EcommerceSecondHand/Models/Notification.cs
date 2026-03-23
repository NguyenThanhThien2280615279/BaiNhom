using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceSecondHand.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;
        
        public NotificationType Type { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ReadAt { get; set; }
        
        public string? RelatedEntityId { get; set; }
        
        public string? ActionUrl { get; set; }
        
        // Navigation property
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
    }
    
    public enum NotificationType
    {
        System,
        Order,
        Message,
        Chat,
        Product,
        Payment,
        Promotion,
        Other,
        Information,  // Keep existing values for compatibility
        Review
    }
}