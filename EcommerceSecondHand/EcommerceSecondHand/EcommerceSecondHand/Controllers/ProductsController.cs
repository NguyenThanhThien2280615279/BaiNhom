using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories.Interfaces;
using EcommerceSecondHand.Filters;
using EcommerceSecondHand.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EcommerceSecondHand.Controllers
{
    [DisallowRole("Admin")]
    public class ProductsController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IReviewRepository _reviewRepository;
        private readonly IOrderRepository _orderRepository;   // 👈 THÊM
        private readonly UserManager<ApplicationUser> _userManager;

        public ProductsController(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IReviewRepository reviewRepository,
            IOrderRepository orderRepository,                 // 👈 THÊM
            UserManager<ApplicationUser> userManager)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _reviewRepository = reviewRepository;
            _orderRepository = orderRepository;               // 👈 THÊM
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(
            int? categoryId = null,
            string? searchQuery = null,
            string? sortOrder = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            ProductCondition? condition = null,
            int page = 1)
        {
            const int pageSize = 12;
            var products = await _productRepository.GetFilteredProductsAsync(
                categoryId, searchQuery, sortOrder, minPrice, maxPrice, condition, page, pageSize);

            var totalCount = await _productRepository.GetFilteredProductsCountAsync(
                categoryId, searchQuery, minPrice, maxPrice, condition);

            var productReviewCounts = new Dictionary<int, int>();
            foreach (var product in products)
            {
                product.AverageRating = await _reviewRepository.GetAverageRatingForProductAsync(product.Id);
                var reviewCount = await _reviewRepository.GetReviewCountByProductAsync(product.Id);
                productReviewCounts[product.Id] = reviewCount;
            }

            var categories = await _categoryRepository.GetAllAsync();

            var model = new ProductListViewModel
            {
                Products = products,
                ProductReviewCounts = productReviewCounts,
                Categories = categories,
                CurrentCategoryId = categoryId,
                SearchQuery = searchQuery,
                SortOrder = sortOrder,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Condition = condition,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                SortOptions = new List<SelectListItem>
                {
                    new SelectListItem { Text = "Mới nhất",         Value = "newest",   Selected = sortOrder == "newest" },
                    new SelectListItem { Text = "Giá thấp đến cao", Value = "priceAsc", Selected = sortOrder == "priceAsc" },
                    new SelectListItem { Text = "Giá cao đến thấp", Value = "priceDesc",Selected = sortOrder == "priceDesc" },
                    new SelectListItem { Text = "Đánh giá cao nhất",Value = "rating",   Selected = sortOrder == "rating" }
                }
            };

            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _productRepository.GetProductWithDetailsAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var reviews = await _reviewRepository.GetReviewsByProductAsync(id);
            var averageRating = await _reviewRepository.GetAverageRatingForProductAsync(id);
            var relatedProducts = await _productRepository.GetRelatedProductsAsync(id, 4);

            bool hasUserReviewed = false;
            bool isOwner = false;
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                hasUserReviewed = await _reviewRepository.HasUserReviewedProductAsync(user.Id, id);
                isOwner = user.Id == product.SellerId;
            }

            var model = new ProductDetailsViewModel
            {
                Product = product,
                Reviews = reviews,
                AverageRating = averageRating,
                RelatedProducts = relatedProducts,
                HasUserReviewed = hasUserReviewed,
                IsOwner = isOwner
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(
            ReviewViewModel model,
            [FromServices] BlockchainReputationService blockchainService
        )
        {
            if (!User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(
                    "Login",
                    "Account",
                    new { returnUrl = Url.Action("Details", "Products", new { id = model.ProductId }) }
                );
            }

            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Details), new { id = model.ProductId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var productForOwnerCheck = await _productRepository.GetByIdAsync(model.ProductId);
            if (productForOwnerCheck == null)
            {
                return NotFound();
            }

            if (productForOwnerCheck.SellerId == user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không thể tự đánh giá sản phẩm của mình.";
                return RedirectToAction(nameof(Details), new { id = model.ProductId });
            }

            // ❗ CHẶN NGƯỜI CHƯA MUA SẢN PHẨM
            var hasPurchased = await UserHasCompletedOrderForProduct(user.Id, model.ProductId);
            if (!hasPurchased)
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể đánh giá sau khi đã mua sản phẩm này.";
                return RedirectToAction(nameof(Details), new { id = model.ProductId });
            }

            bool hasUserReviewed = await _reviewRepository.HasUserReviewedProductAsync(user.Id, model.ProductId);
            if (hasUserReviewed)
            {
                TempData["ErrorMessage"] = "Bạn đã đánh giá sản phẩm này rồi.";
                return RedirectToAction(nameof(Details), new { id = model.ProductId });
            }

            if (model.Rating < 1) model.Rating = 1;
            if (model.Rating > 5) model.Rating = 5;

            var review = new Review
            {
                ProductId = model.ProductId,
                UserId = user.Id,
                Rating = model.Rating,
                Comment = model.Comment,
                DateCreated = DateTime.UtcNow,
                IsApproved = false
            };

            await _reviewRepository.AddAsync(review);
            await _reviewRepository.SaveAsync();

            var productWithSeller = await _productRepository.GetProductWithDetailsAsync(model.ProductId);
            var seller = productWithSeller?.Seller;
            if (seller != null)
            {
                await blockchainService.RecordReviewAndSyncCacheAsync(seller, model.Rating);
            }

            TempData["SuccessMessage"] =
                "Cảm ơn bạn đã đánh giá! Đánh giá của bạn sẽ được hiển thị sau khi được phê duyệt. " +
                "Uy tín on-chain của người bán cũng đã được cập nhật (chế độ demo).";

            return RedirectToAction(nameof(Details), new { id = model.ProductId });
        }

        // ===== Helper: kiểm tra user đã có đơn hoàn tất với sản phẩm hay chưa =====
        private async Task<bool> UserHasCompletedOrderForProduct(string userId, int productId)
        {
            var orders = await _orderRepository.GetUserOrdersAsync(userId);

            // lọc các đơn đã Completed
            var completedOrders = orders.Where(o => o.Status == OrderStatus.Completed).ToList();

            foreach (var ord in completedOrders)
            {
                var fullOrder = await _orderRepository.GetOrderWithItemsAsync(ord.Id);
                if (fullOrder?.OrderItems != null &&
                    fullOrder.OrderItems.Any(oi => oi.ProductId == productId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
