using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceSecondHand.Models
{
    public class RechargePackage
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BonusAmount { get; set; } = 0;
        
        [Required]
        public bool IsActive { get; set; } = true;
        
        [Required]
        public int SortOrder { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<RechargeTransaction>? RechargeTransactions { get; set; }
        
        // Computed properties
        [NotMapped]
        public decimal TotalAmount => Amount + BonusAmount;
        
        [NotMapped]
        public string DisplayName => $"{Name} - {Amount:N0}₫" + (BonusAmount > 0 ? $" (+{BonusAmount:N0}₫)" : "");
    }
}
