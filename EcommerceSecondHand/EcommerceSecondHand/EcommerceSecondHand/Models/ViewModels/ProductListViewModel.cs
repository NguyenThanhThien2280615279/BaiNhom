using EcommerceSecondHand.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class ProductListViewModel
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public IEnumerable<Category> Categories { get; set; } = new List<Category>();
        public int? CurrentCategoryId { get; set; }
        public string? SearchQuery { get; set; }
        public string? SortOrder { get; set; } = "newest";
        public ProductCondition? Condition { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public List<SelectListItem> SortOptions { get; set; } = new List<SelectListItem>();
        public Dictionary<int, int> ProductReviewCounts { get; set; } = new Dictionary<int, int>();
    }
}