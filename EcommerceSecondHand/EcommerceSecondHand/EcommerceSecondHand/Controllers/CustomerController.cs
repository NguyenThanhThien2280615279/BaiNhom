using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using EcommerceSecondHand.Filters;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EcommerceSecondHand.Controllers
{
    [Authorize(Roles = "Customer")]
    [DisallowRole("Admin")]
public class CustomerController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOrderRepository _orderRepository;
        private readonly IReviewRepository _reviewRepository;
        private readonly IPaymentTransactionRepository _transactionRepository;
        private readonly IProductRepository _productRepository;
        private readonly IUserStatisticsRepository _userStatisticsRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly INotificationRepository _notificationRepository;

        public CustomerController(
            UserManager<ApplicationUser> userManager,
            IOrderRepository orderRepository,
            IReviewRepository reviewRepository,
            IPaymentTransactionRepository transactionRepository,
            IProductRepository productRepository,
            IUserStatisticsRepository userStatisticsRepository,
            IMessageRepository messageRepository,
            INotificationRepository notificationRepository)
        {
            _userManager = userManager;
            _orderRepository = orderRepository;
            _reviewRepository = reviewRepository;
            _transactionRepository = transactionRepository;
            _productRepository = productRepository;
            _userStatisticsRepository = userStatisticsRepository;
            _messageRepository = messageRepository;
            _notificationRepository = notificationRepository;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Favorites(int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            const int pageSize = 12;
            
            // Lấy danh sách sản phẩm yêu thích của khách hàng
            var favorites = await _productRepository.GetFavoriteProductsAsync(user.Id, page, pageSize);
            
            // Đếm tổng số sản phẩm yêu thích
            var totalCount = await _productRepository.CountFavoriteProductsAsync(user.Id);

            var model = new ProductListViewModel
            {
                Products = favorites,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToFavorites(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            await _productRepository.AddToFavoritesAsync(user.Id, productId);
            await _productRepository.SaveAsync();

            return RedirectToAction("Details", "Products", new { id = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromFavorites(int productId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            await _productRepository.RemoveFromFavoritesAsync(user.Id, productId);
            await _productRepository.SaveAsync();

            return RedirectToAction(nameof(Favorites));
        }

        public async Task<IActionResult> History(int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            const int pageSize = 12;
            
            // Lấy lịch sử xem sản phẩm của khách hàng
            var viewedProducts = await _productRepository.GetViewHistoryAsync(user.Id, page, pageSize);
            
            // Đếm tổng số sản phẩm đã xem
            var totalCount = await _productRepository.CountViewHistoryAsync(user.Id);

            var model = new ProductListViewModel
            {
                Products = viewedProducts,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearHistory()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            await _productRepository.ClearViewHistoryAsync(user.Id);
            await _productRepository.SaveAsync();

            TempData["SuccessMessage"] = "Lịch sử xem đã được xóa thành công.";
            return RedirectToAction(nameof(History));
        }
    }
}