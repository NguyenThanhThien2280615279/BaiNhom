using EcommerceSecondHand.Models;
using System.ComponentModel.DataAnnotations;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class ProfileViewModel
    {
        public ApplicationUser? User { get; set; }
        public IEnumerable<string> Roles { get; set; } = new List<string>();
        public UserStatistics? UserStatistics { get; set; }
        public IEnumerable<Order> RecentOrders { get; set; } = new List<Order>();
        public IEnumerable<Review> RecentReviews { get; set; } = new List<Review>();
    }
}