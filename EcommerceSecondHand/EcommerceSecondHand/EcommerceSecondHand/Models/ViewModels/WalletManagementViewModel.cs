using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class WalletManagementViewModel
    {
        // ===== Dữ liệu danh sách =====
        public IEnumerable<PaymentTransaction> PendingTransactions { get; set; } = new List<PaymentTransaction>();
        public IEnumerable<PaymentTransaction> RecentTransactions { get; set; } = new List<PaymentTransaction>();

        // ===== Tổng quan =====
        public decimal TotalPendingDeposits { get; set; }        // Escrow
        public decimal TotalPendingWithdrawals { get; set; }     // Chờ chi trả seller
        public decimal TotalSystemBalance { get; set; }          // Số dư hệ thống

        // ✅ Tổng hoa hồng admin (để hiển thị trên thẻ mới)
        public decimal TotalCommissionEarned { get; set; }       // Tổng hoa hồng đã thu
        public decimal CommissionRate { get; set; } = 0m;        // Hiển thị % nếu có cấu hình (vd 0.10 = 10%)

        // ===== Phân trang =====
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 50;

        // ===== Bộ lọc (giữ lại các trường cũ để không phá code hiện tại) =====
        // String (back-compat)
        public string? FilterType { get; set; }                  // ví dụ "Sale", "Purchase", ...
        public string? FilterStatus { get; set; }                // ví dụ "Completed", "Pending", ...
        public DateTime? StartDate { get; set; }                 // từ ngày (inclusive 00:00)
        public DateTime? EndDate { get; set; }                   // đến ngày (inclusive 23:59:59)
        public string? FilterEmail { get; set; }                 // lọc theo email người dùng

        // Strongly-typed (tuỳ chọn sử dụng)
        public TransactionType? FilterTypeEnum { get; set; }
        public TransactionStatus? FilterStatusEnum { get; set; }

        // ===== Tiện ích: chuẩn hoá khoảng ngày (để Controller dùng gọn) =====
        [Display(AutoGenerateField = false)]
        public DateTime? FilterFrom => StartDate?.Date;

        [Display(AutoGenerateField = false)]
        public DateTime? FilterTo =>
    EndDate.HasValue
        ? EndDate.Value.Date.AddDays(1).AddTicks(-1)
        : null;

    }
}
