using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories.Interfaces;
using EcommerceSecondHand.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using EcommerceSecondHand.Hubs;
using System.Linq;

namespace EcommerceSecondHand.Controllers
{
    [Authorize]
    [DisallowRole("Admin")]
    public class CheckoutController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICartRepository _cartRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IPaymentTransactionRepository _transactionRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public CheckoutController(
            UserManager<ApplicationUser> userManager,
            ICartRepository cartRepository,
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            IPaymentTransactionRepository transactionRepository,
            INotificationRepository notificationRepository,
            IHubContext<NotificationHub> notificationHub)
        {
            _userManager = userManager;
            _cartRepository = cartRepository;
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _transactionRepository = transactionRepository;
            _notificationRepository = notificationRepository;
            _notificationHub = notificationHub;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var cartItems = await _cartRepository.GetCartItemsAsync(user.Id);
            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
                return RedirectToAction("Index", "WebCart");
            }

            // Kiểm tra số lượng sản phẩm trong kho
            foreach (var item in cartItems)
            {
                if (item.Product != null && item.Quantity > item.Product.Quantity)
                {
                    TempData["ErrorMessage"] = $"Sản phẩm {item.Product.Name} không đủ số lượng. Vui lòng cập nhật giỏ hàng.";
                    return RedirectToAction("Index", "WebCart");
                }
            }

            // Tính tổng tiền
            var subtotal = cartItems.Sum(item => (item.Product?.Price ?? 0m) * item.Quantity);
            var shippingFee = 0m; // Miễn phí vận chuyển
            var total = subtotal + shippingFee;

            var model = new CheckoutViewModel
            {
                CartItems = cartItems,
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                Total = total,
                ShippingAddress = user.Address,
                ShippingCity = user.City,
                ShippingCountry = user.Country,
                ShippingPostalCode = user.PostalCode,
                PaymentMethod = "Wallet" // Mặc định ví
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var cartItems = await _cartRepository.GetCartItemsAsync(user.Id);
            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
                return RedirectToAction("Index", "WebCart");
            }

            // ✅ Kiểm tra tồn kho theo số khách chọn (KHÔNG trừ kho ở đây)
            foreach (var item in cartItems)
            {
                if (item.Product == null)
                {
                    TempData["ErrorMessage"] = "Có sản phẩm không tồn tại.";
                    return RedirectToAction("Index", "WebCart");
                }

                if (item.Quantity <= 0)
                {
                    TempData["ErrorMessage"] = $"Số lượng đặt cho {item.Product.Name} không hợp lệ.";
                    return RedirectToAction("Index", "WebCart");
                }

                if (item.Product.Quantity < item.Quantity)
                {
                    TempData["ErrorMessage"] = $"Sản phẩm {item.Product.Name} không đủ hàng.";
                    return RedirectToAction("Index", "WebCart");
                }
            }

            // Tính tổng tiền
            var subtotal = cartItems.Sum(ci => (ci.Product?.Price ?? 0m) * ci.Quantity);
            var shippingFee = 0m;
            var total = subtotal + shippingFee;

            // Thanh toán ví (escrow vào Admin như hiện tại)
            if (model.PaymentMethod == "Wallet")
            {
                if (user.WalletBalance < total)
                {
                    TempData["ErrorMessage"] = "Số dư ví của bạn không đủ để thanh toán đơn hàng này.";
                    return RedirectToAction(nameof(Index));
                }

                // Trừ ví người mua
                user.WalletBalance -= total;
                await _userManager.UpdateAsync(user);

                // Cộng ví Admin (tiền tạm giữ)
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                var admin = admins.FirstOrDefault();
                if (admin != null)
                {
                    admin.WalletBalance += total;
                    await _userManager.UpdateAsync(admin);
                }
            }

            // ✅ Tạo đơn hàng (KHÔNG cập nhật kho tại đây)
            var order = new Order
            {
                UserId = user.Id,
                OrderNumber = GenerateOrderNumber(),
                Status = OrderStatus.Pending,
                TotalAmount = total,
                ShippingAddress = model.ShippingAddress,
                ShippingCity = model.ShippingCity,
                ShippingCountry = model.ShippingCountry,
                ShippingPostalCode = model.ShippingPostalCode,
                PaymentMethod = model.PaymentMethod,
                PaymentStatus = PaymentStatus.Paid,
                OrderDate = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OrderItems = new List<OrderItem>()
            };

            foreach (var item in cartItems)
            {
                // ✅ LẤY CHÍNH XÁC SỐ LƯỢNG KHÁCH CHỌN
                var oi = new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,       // KHÔNG được set = item.Product.Quantity
                    Price = item.Product!.Price
                };
                order.OrderItems.Add(oi);

                // Gán người bán (nếu đơn đơn-vendor). Nếu multi-vendor bạn nên tách đơn.
                if (string.IsNullOrEmpty(order.VendorId) && !string.IsNullOrEmpty(item.Product.SellerId))
                {
                    order.VendorId = item.Product.SellerId;
                    order.SellerId = item.Product.SellerId;
                }
            }

            // Lưu đơn
            await _orderRepository.AddAsync(order);
            await _orderRepository.SaveAsync();

            // Giao dịch ví
            if (model.PaymentMethod == "Wallet")
            {
                var purchaseTransaction = new PaymentTransaction
                {
                    UserId = user.Id,
                    Amount = total,
                    Type = TransactionType.Purchase,
                    Status = TransactionStatus.Completed,
                    TransactionReference = $"BUY-ORD-{order.OrderNumber}",
                    Description = $"Thanh toán đơn hàng #{order.OrderNumber}",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    OrderId = order.Id
                };
                await _transactionRepository.AddAsync(purchaseTransaction);

                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                var admin = admins.FirstOrDefault();
                if (admin != null)
                {
                    var escrowTransaction = new PaymentTransaction
                    {
                        UserId = admin.Id,
                        Amount = total,
                        Type = TransactionType.AdminAdjustment,
                        Status = TransactionStatus.Completed,
                        TransactionReference = $"ESCROW-ORD-{order.OrderNumber}",
                        Description = $"Nhận tiền tạm giữ cho đơn #{order.OrderNumber}",
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow,
                        OrderId = order.Id
                    };
                    await _transactionRepository.AddAsync(escrowTransaction);
                }

                await _transactionRepository.SaveAsync();
            }

            // Thông báo cho người mua
            var notification = new Notification
            {
                UserId = user.Id,
                Title = "Đơn hàng đã được đặt",
                Message = $"Đơn hàng #{order.OrderNumber} của bạn đã được đặt thành công.",
                Type = NotificationType.Order,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = order.Id.ToString(),
                ActionUrl = $"/Orders/Details/{order.Id}"
            };
            await _notificationRepository.AddAsync(notification);
            await _notificationHub.Clients.User(user.Id).SendAsync("ReceiveNotification");

            // Thông báo cho người bán
            if (!string.IsNullOrEmpty(order.VendorId))
            {
                var vendorNotification = new Notification
                {
                    UserId = order.VendorId,
                    Title = "Đơn hàng mới",
                    Message = $"Bạn có đơn hàng mới #{order.OrderNumber}.",
                    Type = NotificationType.Order,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedEntityId = order.Id.ToString(),
                    ActionUrl = $"/Vendor/Orders/Details/{order.Id}"
                };
                await _notificationRepository.AddAsync(vendorNotification);
                await _notificationHub.Clients.User(order.VendorId).SendAsync("ReceiveNotification");
            }

            await _notificationRepository.SaveAsync();

            // ✅ Xoá giỏ (không đụng tồn kho)
            await _cartRepository.ClearCartAsync(user.Id);

            return RedirectToAction(nameof(Confirmation), new { id = order.Id });
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var order = await _orderRepository.GetOrderWithDetailsAsync(id);
            if (order == null || order.UserId != user.Id) return NotFound();

            return View(order);
        }

        private string GenerateOrderNumber()
        {
            var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var random = new Random();
            var randomStr = random.Next(1000, 9999).ToString();
            return $"{dateStr}-{randomStr}";
        }
    }
}
