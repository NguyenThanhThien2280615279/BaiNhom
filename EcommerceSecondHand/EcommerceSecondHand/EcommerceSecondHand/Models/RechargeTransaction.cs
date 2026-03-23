using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceSecondHand.Models
{
    public class RechargeTransaction
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public int RechargePackageId { get; set; }
        
        [Required]
        [StringLength(50)]
        public string TransactionCode { get; set; } = string.Empty;
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BonusAmount { get; set; } = 0;
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        
        public RechargeStatus Status { get; set; } = RechargeStatus.Pending;
        
        [StringLength(100)]
        public string? VnPayTransactionId { get; set; }
        
        [StringLength(100)]
        public string? VnPayResponseCode { get; set; }
        
        [StringLength(500)]
        public string? VnPayResponseMessage { get; set; }
        
        [StringLength(1000)]
        public string? VnPaySecureHash { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? CompletedAt { get; set; }
        
        public DateTime? ExpiredAt { get; set; }
        
        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
        
        [ForeignKey("RechargePackageId")]
        public virtual RechargePackage? RechargePackage { get; set; }
    }
    
    public enum RechargeStatus
    {
        Pending = 0,        // Chờ thanh toán
        Processing = 1,     // Đang xử lý
        Completed = 2,      // Hoàn thành
        Failed = 3,         // Thất bại
        Canceled = 4,       // Đã hủy
        Expired = 5         // Hết hạn
    }
}
