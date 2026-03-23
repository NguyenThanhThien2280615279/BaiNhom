using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSecondHand.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrdersController : Controller
    {
        private readonly INotificationRepository _notificationRepository;

        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPaymentTransactionRepository _transactionRepository;

        private const string ErrorKey = "ErrorMessage";
        private const string SuccessKey = "SuccessMessage";

        public OrdersController(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            INotificationRepository notificationRepository,
            UserManager<ApplicationUser> userManager,
            IPaymentTransactionRepository transactionRepository)
        {
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _userManager = userManager;
            _transactionRepository = transactionRepository;
            _notificationRepository = notificationRepository;
        }

        // GET: Admin/Orders
        [HttpGet]
        public async Task<IActionResult> Index(string? search, OrderStatus? status, int page = 1, int pageSize = 20)
        {
            var orders = await _orderRepository.GetFilteredOrdersAsync(search, status, page, pageSize);
            var totalCount = await _orderRepository.CountFilteredOrdersAsync(search, status);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return View(orders);
        }

        // GET: Admin/Orders/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _orderRepository.GetOrderWithDetailsAsync(id); // Include OrderItems + Product
            if (order == null) return NotFound();
            return View(order);
        }

        // POST: Admin/Orders/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
        {
            var order = await _orderRepository.GetOrderWithDetailsAsync(id);
            if (order == null)
            {
                TempData[ErrorKey] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // Không chấp nhận trạng thái Accepted (đã loại bỏ)
            if (status == OrderStatus.Accepted)
            {
                TempData[ErrorKey] = "Trạng thái 'Đã chấp nhận' đã bị loại bỏ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Rule chuyển trạng thái — KHÔNG cho Completed -> Refunded
            bool IsValidTransition(OrderStatus from, OrderStatus to) => (from, to) switch
            {
                (OrderStatus.Pending, OrderStatus.Completed) => true,
                (OrderStatus.Pending, OrderStatus.Canceled) => true,
                // (OrderStatus.Completed, OrderStatus.Refunded) => false // chặn
                _ => false
            };
            if (!IsValidTransition(order.Status, status))
            {
                TempData[ErrorKey] = $"Không thể chuyển {order.Status} → {status}.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // ====== Trừ kho khi Pending -> Completed (idempotent) ======
            if (order.Status == OrderStatus.Pending && status == OrderStatus.Completed)
            {
                // 1) Kiểm tra tồn kho
                foreach (var it in order.OrderItems ?? Enumerable.Empty<OrderItem>())
                {
                    var p = it.Product ?? await _productRepository.GetByIdAsync(it.ProductId);
                    if (p == null || p.Quantity < it.Quantity)
                    {
                        TempData[ErrorKey] = $"Sản phẩm {(p?.Name ?? it.ProductId.ToString())} không đủ hàng.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }

                // 2) Trừ kho
                foreach (var it in order.OrderItems!)
                {
                    var p = it.Product ?? await _productRepository.GetByIdAsync(it.ProductId);
                    p.Quantity -= it.Quantity;
                    if (p.Quantity < 0) p.Quantity = 0;
                    var inStock = p.Quantity > 0;
                    p.IsAvailable = inStock;
                    p.IsActive = inStock;
                    p.UpdatedAt = DateTime.UtcNow;
                    await _productRepository.UpdateAsync(p);
                }
                await _productRepository.SaveAsync();
            }
            // ===========================================================

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;

            try
            {
                // Chia tiền khi hoàn tất (ví), KHÔNG cộng hoa hồng vào số dư hệ thống
                bool needPayout = status == OrderStatus.Completed
                                  && order.PaymentStatus == PaymentStatus.Paid
                                  && string.Equals(order.PaymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase);

                if (needPayout)
                {
                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    var admin = admins.FirstOrDefault();
                    if (admin == null)
                    {
                        TempData[ErrorKey] = "Không có ví hệ thống (Admin).";
                        return RedirectToAction(nameof(Details), new { id });
                    }

                    var orderNo = string.IsNullOrWhiteSpace(order.OrderNumber) ? order.Id.ToString() : order.OrderNumber;
                    var payoutRef = $"PAYOUT-ORD-{orderNo}";
                    var commissionRef = $"COM-ORD-{orderNo}";

                    var existingPayout = await _transactionRepository.GetTransactionByReferenceAsync(payoutRef);
                    var existingCommission = await _transactionRepository.GetTransactionByReferenceAsync(commissionRef);

                    var commission = Math.Round(order.TotalAmount * 0.10m, 2);
                    var vendorAmount = order.TotalAmount - commission;

                    // 1) Trả 90% cho vendor từ ví hệ thống
                    if (existingPayout == null && !string.IsNullOrEmpty(order.VendorId))
                    {
                        var vendor = await _userManager.FindByIdAsync(order.VendorId);
                        if (vendor != null)
                        {
                            if (admin.WalletBalance < vendorAmount)
                            {
                                TempData[ErrorKey] = "Ví hệ thống không đủ để chi trả cho người bán.";
                                return RedirectToAction(nameof(Details), new { id });
                            }

                            admin.WalletBalance -= vendorAmount;
                            vendor.WalletBalance += vendorAmount;
                            await _userManager.UpdateAsync(admin);
                            await _userManager.UpdateAsync(vendor);

                            await _transactionRepository.AddAsync(new PaymentTransaction
                            {
                                UserId = vendor.Id,
                                Amount = vendorAmount,
                                Type = TransactionType.Sale,
                                Status = TransactionStatus.Completed,
                                TransactionReference = payoutRef,
                                Description = $"Nhận tiền bán đơn #{orderNo} (90%)",
                                CreatedAt = DateTime.UtcNow,
                                CompletedAt = DateTime.UtcNow,
                                OrderId = order.Id,
                                PaymentMethod = "Wallet"
                            });
                        }
                    }

                    // 2) Ghi nhận hoa hồng 10% (chỉ ghi transaction, KHÔNG cộng vào số dư hệ thống)
                    if (existingCommission == null)
                    {
                        await _transactionRepository.AddAsync(new PaymentTransaction
                        {
                            UserId = admin.Id,
                            Amount = commission,
                            Type = TransactionType.AdminAdjustment,
                            Status = TransactionStatus.Completed,
                            TransactionReference = commissionRef,
                            Description = $"Hoa hồng 10% đơn #{orderNo}",
                            CreatedAt = DateTime.UtcNow,
                            CompletedAt = DateTime.UtcNow,
                            OrderId = order.Id,
                            PaymentMethod = "Wallet"
                        });
                    }

                    await _transactionRepository.SaveAsync();
                }

                await _orderRepository.UpdateAsync(order);
                await _orderRepository.SaveAsync();
            }
            catch
            {
                TempData[ErrorKey] = "Không thể cập nhật trạng thái đơn hàng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData[SuccessKey] = "Đã cập nhật trạng thái đơn hàng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Admin/Orders/CancelOrder/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null)
            {
                TempData[ErrorKey] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                TempData[ErrorKey] = "Phiên đăng nhập không hợp lệ.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = OrderStatus.Canceled;
            order.CanceledByUserId = admin.Id;
            order.CanceledAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveAsync();

            TempData[SuccessKey] = "✅ Đơn đã bị Admin hủy.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelivery(
    int id,
    [FromServices] EcommerceSecondHand.Services.BlockchainReputationService blockchainService
)
        {
            // 1. Lấy người mua (currentUser)
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return NotFound();

            // 2. Lấy order kèm items
            var order = await _orderRepository.GetOrderWithItemsAsync(id);
            if (order == null || order.UserId != currentUser.Id)
                return NotFound();

            // 3. Chặn các trạng thái không hợp lệ
            if (order.Status == OrderStatus.Canceled || order.Status == OrderStatus.Refunded)
            {
                TempData["ErrorMessage"] = "Đơn này đã bị hủy hoặc đã hoàn tiền.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // 4. Nếu nó đã Completed rồi thì đừng chạy tiếp nữa (tránh double click)
            var wasAlreadyCompleted = (order.Status == OrderStatus.Completed);

            // 5. Đánh dấu hoàn tất
            order.Status = OrderStatus.Completed;
            order.DeliveredAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveAsync();

            // 6. TIỀN: áp dụng cùng logic bạn dùng bên Admin.UpdateStatus
            //    => Giải phóng escrow từ ví admin sang người bán, trừ 10% hoa hồng
            //    Chỉ làm nếu:
            //      - Thanh toán đã Paid
            //      - Phương thức là Wallet
            //      - Chưa xử lý payout trước đó
            if (!wasAlreadyCompleted // chỉ chạy nếu trước đó chưa completed
                && order.PaymentStatus == PaymentStatus.Paid
                && string.Equals(order.PaymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase))
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                var admin = admins.FirstOrDefault();
                if (admin == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy ví hệ thống (Admin).";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var orderNo = string.IsNullOrWhiteSpace(order.OrderNumber)
                    ? order.Id.ToString()
                    : order.OrderNumber;

                var payoutRef = $"PAYOUT-ORD-{orderNo}";
                var commissionRef = $"COM-ORD-{orderNo}";

                // Kiểm tra xem đã tạo giao dịch trước đó chưa
                var existingPayout = await _transactionRepository.GetTransactionByReferenceAsync(payoutRef);
                var existingCommission = await _transactionRepository.GetTransactionByReferenceAsync(commissionRef);

                // Tính toán chia tiền
                var commission = Math.Round(order.TotalAmount * 0.10m, 2);       // phí 10%
                var vendorAmount = order.TotalAmount - commission;               // 90%

                // 6.1 Trả tiền cho người bán (vendor)
                if (existingPayout == null && !string.IsNullOrEmpty(order.VendorId))
                {
                    var vendor = await _userManager.FindByIdAsync(order.VendorId);
                    if (vendor != null)
                    {
                        // Admin đang giữ escrow => admin phải có ít nhất vendorAmount
                        if (admin.WalletBalance < vendorAmount)
                        {
                            // Nếu admin ví không đủ -> vấn đề luồng tiền
                            TempData["ErrorMessage"] = "Ví hệ thống không đủ để chi trả cho người bán.";
                            return RedirectToAction(nameof(Details), new { id });
                        }

                        // Chuyển tiền: admin trả vendor phần 90%
                        admin.WalletBalance -= vendorAmount;
                        vendor.WalletBalance += vendorAmount;

                        await _userManager.UpdateAsync(admin);
                        await _userManager.UpdateAsync(vendor);

                        // Log giao dịch payout cho vendor
                        await _transactionRepository.AddAsync(new PaymentTransaction
                        {
                            UserId = vendor.Id,
                            Amount = vendorAmount,
                            Type = TransactionType.Sale, // giống bên Admin
                            Status = TransactionStatus.Completed,
                            TransactionReference = payoutRef,
                            Description = $"Nhận tiền bán đơn #{orderNo} (90%)",
                            CreatedAt = DateTime.UtcNow,
                            CompletedAt = DateTime.UtcNow,
                            OrderId = order.Id,
                            PaymentMethod = "Wallet"
                        });
                    }
                }

                // 6.2 Ghi giao dịch hoa hồng 10% cho admin (chỉ log kế toán, không cộng thêm tiền 1 lần nữa)
                if (existingCommission == null)
                {
                    await _transactionRepository.AddAsync(new PaymentTransaction
                    {
                        UserId = admin.Id,
                        Amount = commission,
                        Type = TransactionType.AdminAdjustment,
                        Status = TransactionStatus.Completed,
                        TransactionReference = commissionRef,
                        Description = $"Hoa hồng 10% đơn #{orderNo}",
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow,
                        OrderId = order.Id,
                        PaymentMethod = "Wallet"
                    });
                }

                // Lưu các transaction vừa add
                await _transactionRepository.SaveAsync();
            }

            // 7. Uy tín on-chain (mock hoặc real)
            if (!string.IsNullOrEmpty(order.SellerId))
            {
                var seller = await _userManager.FindByIdAsync(order.SellerId);
                if (seller != null)
                {
                    await blockchainService.RecordSuccessfulOrderAndSyncCacheAsync(order, seller);
                }
            }

            // 8. Tạo thông báo cho buyer
            var buyerNotify = new Notification
            {
                UserId = currentUser.Id,
                Title = "Đơn hàng đã hoàn tất",
                Message = $"Bạn đã xác nhận đã nhận đơn #{order.OrderNumber}. Cảm ơn bạn!",
                Type = NotificationType.Order,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = order.Id.ToString(),
                ActionUrl = $"/Orders/Details/{order.Id}"
            };
            await _notificationRepository.AddAsync(buyerNotify);

            // 9. Tạo thông báo cho seller
            if (!string.IsNullOrEmpty(order.SellerId))
            {
                var sellerNotify = new Notification
                {
                    UserId = order.SellerId,
                    Title = "Bạn đã nhận tiền bán hàng",
                    Message = $"Đơn #{order.OrderNumber} đã hoàn tất. Tiền đã được chuyển vào ví của bạn sau khi trừ 10% phí.",
                    Type = NotificationType.Order,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedEntityId = order.Id.ToString(),
                    ActionUrl = $"/Vendor/Orders/Details/{order.Id}"
                };
                await _notificationRepository.AddAsync(sellerNotify);
            }

            await _notificationRepository.SaveAsync();

            TempData["SuccessMessage"] = "Đơn hàng đã xác nhận hoàn tất. Tiền đã được thanh toán cho người bán.";
            return RedirectToAction(nameof(Details), new { id });
        }

    }
}
