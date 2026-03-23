using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EcommerceSecondHand.Filters;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EcommerceSecondHand.Controllers
{
    [Authorize(Roles = "Vendor")]
    [DisallowRole("Admin")]
    public class VendorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProductRepository _productRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IReviewRepository _reviewRepository;
        private readonly IPaymentTransactionRepository _transactionRepository;
        private readonly IUserStatisticsRepository _userStatisticsRepository;
        private readonly ICategoryRepository _categoryRepository;

        public VendorController(
      UserManager<ApplicationUser> userManager,
      IProductRepository productRepository,
      IOrderRepository orderRepository,
      IReviewRepository reviewRepository,
      IPaymentTransactionRepository transactionRepository,
      IUserStatisticsRepository userStatisticsRepository,
      ICategoryRepository categoryRepository)
        {
            _userManager = userManager;
            _productRepository = productRepository;
            _orderRepository = orderRepository;
            _reviewRepository = reviewRepository;
            _transactionRepository = transactionRepository;
            _userStatisticsRepository = userStatisticsRepository;
            _categoryRepository = categoryRepository;
        }

        // ================= DASHBOARD =================
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var statistics = await _userStatisticsRepository.GetByUserIdAsync(user.Id);
            var recentProducts = await _productRepository.GetRecentProductsBySellerAsync(user.Id, 5);
            var recentOrders = await _orderRepository.GetRecentOrdersForVendorByItemsAsync(user.Id, 5);
            var recentReviews = await _reviewRepository.GetRecentReviewsBySellerAsync(user.Id, 5);
            var recentTransactions = await _transactionRepository.GetRecentTransactionsByUserAsync(user.Id, 5);
            var totalProducts = await _productRepository.CountProductsBySellerAsync(user.Id);
            var totalSales = await _orderRepository.CountOrdersByVendorAsync(user.Id);
            var totalRevenue = await _orderRepository.GetTotalRevenueByVendorAsync(user.Id);
            var averageRating = await _reviewRepository.GetAverageRatingBySellerAsync(user.Id);
            var pendingOrders = await _orderRepository.CountPendingOrdersByVendorAsync(user.Id);

            var model = new VendorDashboardViewModel
            {
                User = user,
                Statistics = statistics,
                RecentProducts = recentProducts,
                RecentOrders = recentOrders,
                RecentReviews = recentReviews,
                RecentTransactions = recentTransactions,
                TotalProducts = totalProducts,
                TotalSales = totalSales,
                TotalRevenue = totalRevenue,
                AverageRating = (decimal)averageRating,
                PendingOrders = pendingOrders
            };

            return View(model);
        }

        // ================= PRODUCT LIST =================
        public async Task<IActionResult> Products(string sortOrder = "newest", int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            const int pageSize = 10;

            var products = await _productRepository.GetProductsBySellerAsync(user.Id, sortOrder, page, pageSize);
            var totalCount = await _productRepository.CountProductsBySellerAsync(user.Id);
            var categories = await _categoryRepository.GetAllAsync();

            var model = new ProductListViewModel
            {
                Products = products,
                Categories = categories,
                SortOrder = sortOrder,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                SortOptions = new System.Collections.Generic.List<SelectListItem>
                {
                    new SelectListItem { Text = "Mới nhất", Value = "newest", Selected = sortOrder == "newest" },
                    new SelectListItem { Text = "Cũ nhất", Value = "oldest", Selected = sortOrder == "oldest" },
                    new SelectListItem { Text = "Giá tăng dần", Value = "priceAsc", Selected = sortOrder == "priceAsc" },
                    new SelectListItem { Text = "Giá giảm dần", Value = "priceDesc", Selected = sortOrder == "priceDesc" }
                }
            };

            return View(model);
        }

        public async Task<IActionResult> SoldProducts(int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            const int pageSize = 10;

            var products = await _productRepository.GetSoldProductsByVendorAsync(user.Id, page, pageSize);
            var totalCount = await _productRepository.CountSoldProductsByVendorAsync(user.Id);

            var model = new ProductListViewModel
            {
                Products = products,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return View("Products", model);
        }

        // ================= ORDERS LIST & DETAILS =================
        public async Task<IActionResult> Orders(OrderStatus? status = null, string sortOrder = "newest", int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            const int pageSize = 10;

            var orders = await _orderRepository.GetOrdersByVendorAsync(user.Id, status, sortOrder, page, pageSize);
            var totalCount = await _orderRepository.CountOrdersByVendorAsync(user.Id, status);

            var model = new OrderListViewModel
            {
                Orders = orders,
                Status = status,
                SortOrder = sortOrder,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return View(model);
        }

        // ✅ Dùng route chuẩn cho Vendor xem chi tiết đơn
        [HttpGet("Vendor/Orders/Details/{id}")]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var order = await _orderRepository.GetOrderWithDetailsAsync(id);
            bool hasAccess = order != null && (
                string.Equals(order.SellerId, user.Id, StringComparison.Ordinal) ||
                string.Equals(order.VendorId, user.Id, StringComparison.Ordinal) ||
                (order.OrderItems != null && order.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == user.Id))
            );

            if (!hasAccess)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn này.";
                return RedirectToAction(nameof(Orders));
            }

            return View("OrderDetails", order);
        }


        // ================= ACCEPT ORDER (TRỪ KHO) =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOrder(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var order = await _orderRepository.GetOrderWithDetailsAsync(id);
            if (order == null || order.SellerId != user.Id) return NotFound();

            if (order.Status != OrderStatus.Pending)
            {
                TempData["ErrorMessage"] = "Chỉ có thể chấp nhận đơn hàng đang chờ.";
                return RedirectToAction("OrderDetails", "Vendor", new { id });
            }

            // 1) Kiểm tra tồn kho đủ
            foreach (var it in order.OrderItems ?? Enumerable.Empty<OrderItem>())
            {
                var p = it.Product ?? await _productRepository.GetByIdAsync(it.ProductId);
                if (p == null || p.Quantity < it.Quantity)
                {
                    TempData["ErrorMessage"] = $"Sản phẩm {(p?.Name ?? it.ProductId.ToString())} không đủ hàng.";
                    return RedirectToAction("OrderDetails", "Vendor", new { id });
                }
            }

            // 2) Trừ kho
            foreach (var it in order.OrderItems)
            {
                var p = it.Product ?? await _productRepository.GetByIdAsync(it.ProductId);
                p.Quantity -= it.Quantity;
                p.IsAvailable = p.Quantity > 0;
                p.IsActive = p.Quantity > 0;
                p.UpdatedAt = DateTime.UtcNow;
                await _productRepository.UpdateAsync(p);
            }
            await _productRepository.SaveAsync();

            order.Status = OrderStatus.Accepted;
            order.UpdatedAt = DateTime.UtcNow;
            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveAsync();

            TempData["SuccessMessage"] = "Đơn hàng đã được chấp nhận.";
            return RedirectToAction("OrderDetails", "Vendor", new { id });
        }

        // ================= CANCEL ORDER (TRẢ KHO + HOÀN TIỀN) =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var order = await _orderRepository.GetOrderWithDetailsAsync(id);
            if (order == null || (order.SellerId != user.Id && order.VendorId != user.Id))
                return NotFound();

            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Accepted)
            {
                TempData["ErrorMessage"] = "Chỉ có thể hủy đơn hàng ở trạng thái Pending hoặc Accepted.";
                return RedirectToAction("OrderDetails", "Vendor", new { id });
            }

            order.Status = OrderStatus.Canceled;
            order.UpdatedAt = DateTime.UtcNow;

            // Hoàn tiền nếu đã thanh toán
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                var buyer = await _userManager.FindByIdAsync(order.UserId);
                if (buyer != null)
                {
                    buyer.WalletBalance += order.TotalAmount;
                    await _userManager.UpdateAsync(buyer);
                }

                var refundTx = new PaymentTransaction
                {
                    UserId = order.UserId,
                    Amount = order.TotalAmount,
                    Type = TransactionType.Refund,
                    Status = TransactionStatus.Completed,
                    TransactionReference = $"REF-ORD-{order.OrderNumber}",
                    Description = $"Hoàn tiền đơn hàng #{order.OrderNumber}",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    OrderId = order.Id
                };
                await _transactionRepository.AddAsync(refundTx);
                await _transactionRepository.SaveAsync();

                order.PaymentStatus = PaymentStatus.Refunded;
            }

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveAsync();

            // Trả kho nếu trước đó đã trừ (Accepted)
            if (order.OrderItems != null)
            {
                foreach (var item in order.OrderItems)
                {
                    var product = item.Product ?? await _productRepository.GetByIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Quantity += item.Quantity;
                        product.IsAvailable = product.Quantity > 0;
                        product.IsActive = product.Quantity > 0;
                        product.UpdatedAt = DateTime.UtcNow;
                        await _productRepository.UpdateAsync(product);
                    }
                }
                await _productRepository.SaveAsync();
            }

            TempData["SuccessMessage"] = "Đơn hàng đã được hủy.";
            return RedirectToAction("OrderDetails", "Vendor", new { id });
        }

        // ================= ADD PRODUCT =================
        public async Task<IActionResult> AddProduct()
        {
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View(new AddProductViewModel { Condition = ProductCondition.New });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProduct(AddProductViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name", model.CategoryId);
                return View(model);
            }

            // Upload ảnh (nếu có)
            string imageUrl = null;
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(model.ImageFile.FileName)}";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products", fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                imageUrl = $"/uploads/products/{fileName}";
            }

            bool inStock = model.Quantity > 0;

            var product = new Product
            {
                Name = model.Name,
                Description = model.Description,
                Price = model.Price,
                Quantity = model.Quantity,
                CategoryId = model.CategoryId,
                Condition = model.Condition,
                SellerId = user.Id,
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsAvailable = inStock,
                IsActive = inStock
            };

            await _productRepository.AddAsync(product);
            await _productRepository.SaveAsync();

            TempData["SuccessMessage"] = "Sản phẩm đã được thêm thành công.";
            return RedirectToAction(nameof(Products));
        }

        // ================= EDIT PRODUCT =================
        public async Task<IActionResult> EditProduct(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null || product.SellerId != user.Id) return NotFound();

            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);

            var model = new EditProductViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                CategoryId = product.CategoryId,
                Condition = product.Condition,
                Quantity = product.Quantity,
                CurrentImageUrl = product.ImageUrl
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(EditProductViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var product = await _productRepository.GetByIdAsync(model.Id);
            if (product == null || product.SellerId != user.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                model.CurrentImageUrl = product.ImageUrl;
                var categories = await _categoryRepository.GetAllAsync();
                ViewBag.Categories = new SelectList(categories, "Id", "Name", model.CategoryId);
                return View(model);
            }

            // Upload ảnh mới (nếu có)
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(model.ImageFile.FileName)}";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products", fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                product.ImageUrl = $"/uploads/products/{fileName}";
            }

            // Cập nhật thông tin
            product.Name = model.Name;
            product.Description = model.Description;
            product.Price = model.Price;
            product.CategoryId = model.CategoryId;
            product.Condition = model.Condition;
            product.Quantity = Math.Max(0, model.Quantity);

            bool inStock = product.Quantity > 0;
            product.IsAvailable = inStock;
            product.IsActive = inStock;

            product.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(product);
            await _productRepository.SaveAsync();

            TempData["SuccessMessage"] = "Sản phẩm đã được cập nhật thành công.";
            return RedirectToAction(nameof(Products));
        }

        // ================= DELETE PRODUCT =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null || product.SellerId != user.Id)
                return NotFound();

            var message = await _productRepository.DeleteAsync(id);

            if (message.Contains("thành công", StringComparison.OrdinalIgnoreCase))
                TempData["SuccessMessage"] = message;
            else
                TempData["ErrorMessage"] = message;

            return RedirectToAction(nameof(Products));
        }
    }
}
