using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceSecondHand.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        public string OrderNumber { get; set; } = string.Empty;
        
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        
        [Required]
        public string ShippingAddress { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string? ShippingCity { get; set; }
        
        [StringLength(100)]
        public string? ShippingCountry { get; set; }
        
        [StringLength(20)]
        public string? ShippingPostalCode { get; set; }
        
        [Required]
        public string PaymentMethod { get; set; } = string.Empty;
        
        public string? VendorId { get; set; }
        
        public string? SellerId { get; set; }
        
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ShippedAt { get; set; }
        
        public DateTime? DeliveredAt { get; set; }

        // NEW: ai là người hủy + thời điểm hủy
        public string? CanceledByUserId { get; set; }   // NEW
        public DateTime? CanceledAt { get; set; }       // NEW

        // Thuộc tính điều hướng
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
        
        [ForeignKey("SellerId")]
        public ApplicationUser? Seller { get; set; }
        
        public ICollection<OrderItem>? OrderItems { get; set; }
    }
    
    public enum OrderStatus
    {
        Pending = 0,
        Accepted = 1,
        Canceled = 2,
        Refunded = 3,
        Completed = 4
    }

    public enum PaymentStatus
    {
        Pending,
        Paid,
        Failed,
        Refunded
    }
}