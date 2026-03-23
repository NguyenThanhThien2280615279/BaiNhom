using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class RechargeViewModel
    {
        public IEnumerable<RechargePackage> Packages { get; set; } = new List<RechargePackage>();
        public decimal CurrentBalance { get; set; }
        public IEnumerable<RechargeTransaction> RecentTransactions { get; set; } = new List<RechargeTransaction>();
    }
    
    public class RechargePackageViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BonusAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSelected { get; set; }
    }
    
    public class RechargeTransactionViewModel
    {
        public int Id { get; set; }
        public string TransactionCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal BonusAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public RechargeStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string PackageName { get; set; } = string.Empty;
    }
}
