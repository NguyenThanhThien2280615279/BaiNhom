using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceSecondHand.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? ProfilePicture { get; set; }
        public string? Bio { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginDate { get; set; }
        public DateTime? LastActiveAt { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal WalletBalance { get; set; } = 0;

        // ---- Blockchain reputation cache ----
        // địa chỉ ví blockchain công khai của seller
        public string? BlockchainAddress { get; set; }

        // tổng số đơn đã giao thành công (được confirm)
        public int OnChainSuccessfulSales { get; set; } = 0;

        // điểm đánh giá trung bình (1..5) lấy từ chain
        public double OnChainAverageRating { get; set; } = 0;

        // cấp độ uy tín: 0 = none, 1 = Bronze, 2 = Silver, 3 = Gold
        public byte OnChainBadgeLevel { get; set; } = 0;

        // Thu?c tính b? sung cho vai tṛ và hi?n th? ng??i dùng
        public List<string> Roles { get; set; } = new List<string>();
        
        // Thu?c tính cho ch? ?? xem tin nh?n
        public int UnreadMessagesCount { get; set; } = 0;
        public Message? LastMessage { get; set; }

        // Thu?c tính ?i?u h??ng
        public virtual ICollection<Order>? Orders { get; set; }
        public virtual ICollection<Product>? Products { get; set; }
        public virtual ICollection<Review>? Reviews { get; set; }
        public virtual ICollection<CartItem>? CartItems { get; set; }
        public virtual ICollection<Message>? SentMessages { get; set; }
        public virtual ICollection<Message>? ReceivedMessages { get; set; }
        public virtual ICollection<Notification>? Notifications { get; set; }
        public virtual ICollection<PaymentTransaction>? PaymentTransactions { get; set; }
    }
}