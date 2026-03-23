using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class ReviewListViewModel
    {
        public IEnumerable<Review> Reviews { get; set; } = new List<Review>();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
    }
}