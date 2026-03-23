using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class AdminUserDetailsViewModel
    {
        public ApplicationUser User { get; set; } = null!;
        public IList<string> Roles { get; set; } = new List<string>();
        public IEnumerable<Order> Orders { get; set; } = new List<Order>();
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public IEnumerable<Review> Reviews { get; set; } = new List<Review>();
        public IEnumerable<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
    }
}
