using System;
using System.ComponentModel.DataAnnotations;

namespace EcommerceSecondHand.Models
{
    public class OnChainLog
    {
        [Key]
        public int Id { get; set; }

        // Địa chỉ ví (0x....) của seller
        [Required]
        public string Address { get; set; } = string.Empty;

        // Loại event: OrderRecorded | ReviewRecorded | Commission | ...
        [Required]
        public string EventType { get; set; } = string.Empty;

        // Mã đơn, nếu có
        public string? OrderNumber { get; set; }

        // TxHash mô phỏng hoặc thật
        public string? TxHash { get; set; }

        // Thêm mô tả / debug
        public string? Details { get; set; }

        // Số tiền liên quan (VD: tổng đơn)
        public decimal? Amount { get; set; }

        // Thời điểm ghi nhận
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
