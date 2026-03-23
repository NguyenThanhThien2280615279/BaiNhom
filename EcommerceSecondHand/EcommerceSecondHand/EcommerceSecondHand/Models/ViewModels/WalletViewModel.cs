using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class WalletViewModel
    {
        public decimal Balance { get; set; }
        public IEnumerable<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public string? FilterType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}