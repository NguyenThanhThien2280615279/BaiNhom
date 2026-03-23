using System;
using System.Linq;
using System.Threading.Tasks;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using EcommerceSecondHand.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSecondHand.Controllers
{
    [Authorize]
    [DisallowRole("Admin")]
    public class OrdersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IPaymentTransactionRepository _transactionRepository;
        private readonly IReviewRepository _reviewRepository;   // 👈 THÊM

        public OrdersController(
            UserManager<ApplicationUser> _userManager,
            IOrderRepository orderRepository,
            INotificationRepository notificationRepository,
            IPaymentTransactionRepository transactionRepository,
            IProductRepository productRepository,
            IReviewRepository reviewRepository                // 👈 THÊM
        )
        {
            this._userManager = _userManager;
            _orderRepository = orderRepository;
            _notificationRepository = notificationRepository;
            _transactionRepository = transactionRepository;
            _productRepository = productRepository;
            _reviewRepository = reviewRepository;             // 👈 THÊM
        }

        // ======================== DANH SÁCH ĐƠN ========================

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var orders = await _orderRepository.GetUserOrdersAsync(user.Id);
            return View(orders);
        }

        // ======================== CHI TIẾT ĐƠN ========================

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var order = await _orderRepository.GetOrderWithDetailsAsync(id);
            if (order == null || order.UserId != user.Id)
            {
                return NotFound();
            }

            return View(order);
        }

        // ======================== HỦY ĐƠN ========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var order = await _orderRepository.GetOrderWithItemsAsync(id);
            if (order == null || order.UserId != user.Id)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Accepted)
            {
                TempData["ErrorMessage"] = "Đơn hàng này không thể hủy.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = OrderStatus.Canceled;
            order.UpdatedAt = DateTime.UtcNow;

            if (order.PaymentStatus == PaymentStatus.Paid &&
                string.Equals(order.PaymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase))
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
                    TransactionReference = $"CXL-ORD-{order.OrderNumber}",
                    Description = $"Hoàn tiền hủy đơn #{order.OrderNumber}",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    OrderId = order.Id
                };
                await _transactionRepository.AddAsync(refundTx);

                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                var admin = admins.FirstOrDefault();
                if (admin != null)
                {
                    if (admin.WalletBalance >= order.TotalAmount)
                    {
                        admin.WalletBalance -= order.TotalAmount;
                        await _userManager.UpdateAsync(admin);

                        var adminReleaseTx = new PaymentTransaction
                        {
                            UserId = admin.Id,
                            Amount = order.TotalAmount,
                            Type = TransactionType.Withdraw,
                            Status = TransactionStatus.Completed,
                            TransactionReference = $"ESCROW-RELEASE-ORD-{order.OrderNumber}",
                            Description = $"Giải phóng escrow khi hủy đơn #{order.OrderNumber}",
                            CreatedAt = DateTime.UtcNow,
                            CompletedAt = DateTime.UtcNow,
                            OrderId = order.Id
                        };
                        await _transactionRepository.AddAsync(adminReleaseTx);
                    }
                }

                await _transactionRepository.SaveAsync();

                order.PaymentStatus = PaymentStatus.Refunded;
            }

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveAsync();

            if (order.OrderItems != null)
            {
                foreach (var item in order.OrderItems)
                {
                    var product = item.Product ?? await _productRepository.GetByIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Quantity = Math.Max(0, product.Quantity) + Math.Max(1, item.Quantity);
                        product.IsAvailable = true;
                        product.IsActive = true;
                        product.UpdatedAt = DateTime.UtcNow;
                        await _productRepository.UpdateAsync(product);
                    }
                }
                await _productRepository.SaveAsync();
            }

            var notification = new Notification
            {
                UserId = user.Id,
                Title = "Đơn hàng đã bị hủy",
                Message = $"Đơn hàng #{order.OrderNumber} đã bị hủy thành công.",
                Type = NotificationType.Order,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = order.Id.ToString(),
                ActionUrl = $"/Orders/Details/{order.Id}"
            };

            await _notificationRepository.AddAsync(notification);
            await _notificationRepository.SaveAsync();

            if (!string.IsNullOrEmpty(order.VendorId))
            {
                var vendorNotification = new Notification
                {
                    UserId = order.VendorId,
                    Title = "Đơn hàng đã bị hủy",
                    Message = $"Đơn hàng #{order.OrderNumber} đã bị khách hàng hủy.",
                    Type = NotificationType.Order,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedEntityId = order.Id.ToString(),
                    ActionUrl = $"/Vendor/Orders/Details/{order.Id}"
                };

                await _notificationRepository.AddAsync(vendorNotification);
                await _notificationRepository.SaveAsync();
            }

            TempData["SuccessMessage"] = "Đơn hàng đã được hủy thành công.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ======================== TRẢ HÀNG ========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestReturn(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var order = await _orderRepository.GetByIdAsync(id);
            if (order == null || order.UserId != user.Id)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Accepted)
            {
                TempData["ErrorMessage"] = "Chỉ có thể trả hàng khi đơn ở trạng thái Accepted.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = OrderStatus.Refunded;
            order.UpdatedAt = DateTime.UtcNow;

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
                    TransactionReference = $"RET-ORD-{order.OrderNumber}",
                    Description = $"Khách trả hàng đơn #{order.OrderNumber}",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    OrderId = order.Id
                };
                await _transactionRepository.AddAsync(refundTx);

                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                var admin = admins.FirstOrDefault();
                if (admin != null)
                {
                    if (admin.WalletBalance >= order.TotalAmount)
                    {
                        admin.WalletBalance -= order.TotalAmount;
                        await _userManager.UpdateAsync(admin);

                        var adminReleaseTx = new PaymentTransaction
                        {
                            UserId = admin.Id,
                            Amount = order.TotalAmount,
                            Type = TransactionType.Withdraw,
                            Status = TransactionStatus.Completed,
                            TransactionReference = $"ESCROW-RELEASE-ORD-{order.OrderNumber}",
                            Description = $"Giải phóng escrow khi trả hàng #{order.OrderNumber}",
                            CreatedAt = DateTime.UtcNow,
                            CompletedAt = DateTime.UtcNow,
                            OrderId = order.Id
                        };
                        await _transactionRepository.AddAsync(adminReleaseTx);
                    }
                }

                await _transactionRepository.SaveAsync();
            }

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveAsync();

            var notification = new Notification
            {
                UserId = user.Id,
                Title = "Yêu cầu trả hàng đã xử lý",
                Message = $"Đơn hàng #{order.OrderNumber} đã được hoàn tiền.",
                Type = NotificationType.Order,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = order.Id.ToString(),
                ActionUrl = $"/Orders/Details/{order.Id}"
            };
            await _notificationRepository.AddAsync(notification);
            await _notificationRepository.SaveAsync();

            if (!string.IsNullOrEmpty(order.VendorId))
            {
                var vendorNotification = new Notification
                {
                    UserId = order.VendorId,
                    Title = "Đơn hàng bị trả",
                    Message = $"Khách đã trả đơn #{order.OrderNumber}.",
                    Type = NotificationType.Order,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedEntityId = order.Id.ToString(),
                    ActionUrl = $"/Vendor/Orders/Details/{order.Id}"
                };

                await _notificationRepository.AddAsync(vendorNotification);
                await _notificationRepository.SaveAsync();
            }

            TempData["SuccessMessage"] = "Đã hoàn tiền đơn hàng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ======================== XÁC NHẬN ĐÃ NHẬN HÀNG ========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelivery(
            int id,
            [FromServices] EcommerceSecondHand.Services.BlockchainReputationService blockchainService
        )
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return NotFound();

            var order = await _orderRepository.GetOrderWithItemsAsync(id);
            if (order == null || order.UserId != currentUser.Id)
                return NotFound();

            if (order.Status == OrderStatus.Canceled || order.Status == OrderStatus.Refunded)
            {
                TempData["ErrorMessage"] = "Đơn này đã bị hủy hoặc đã hoàn tiền.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var oldStatus = order.Status;

            order.Status = OrderStatus.Completed;
            order.DeliveredAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveAsync();

            if (oldStatus == OrderStatus.Pending)
            {
                foreach (var it in order.OrderItems ?? Enumerable.Empty<OrderItem>())
                {
                    var p = it.Product ?? await _productRepository.GetByIdAsync(it.ProductId);
                    if (p == null || p.Quantity < it.Quantity)
                    {
                        TempData["ErrorMessage"] =
                            $"Sản phẩm {(p?.Name ?? it.ProductId.ToString())} không đủ hàng trong kho.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }

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

            if (order.PaymentStatus == PaymentStatus.Paid
                && string.Equals(order.PaymentMethod, "Wallet", StringComparison.OrdinalIgnoreCase))
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                var admin = admins.FirstOrDefault();
                if (admin == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy ví hệ thống (Admin).";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var vendorId = order.VendorId;
                ApplicationUser? vendor = null;
                if (!string.IsNullOrEmpty(vendorId))
                {
                    vendor = await _userManager.FindByIdAsync(vendorId);
                }

                var orderNo = string.IsNullOrWhiteSpace(order.OrderNumber)
                    ? order.Id.ToString()
                    : order.OrderNumber;

                var payoutRef = $"PAYOUT-ORD-{orderNo}";
                var commissionRef = $"COM-ORD-{orderNo}";

                var existingPayout = await _transactionRepository.GetTransactionByReferenceAsync(payoutRef);
                var existingCommission = await _transactionRepository.GetTransactionByReferenceAsync(commissionRef);

                var commission = Math.Round(order.TotalAmount * 0.10m, 2);
                var vendorAmount = order.TotalAmount - commission;

                if (existingPayout == null && vendor != null)
                {
                    if (admin.WalletBalance < vendorAmount)
                    {
                        TempData["ErrorMessage"] = "Ví hệ thống không đủ để chi trả cho người bán.";
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

            if (!string.IsNullOrEmpty(order.SellerId))
            {
                var sellerForRep = await _userManager.FindByIdAsync(order.SellerId);
                if (sellerForRep != null)
                {
                    await blockchainService.RecordSuccessfulOrderAndSyncCacheAsync(order, sellerForRep);
                }
            }

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

            if (!string.IsNullOrEmpty(order.VendorId))
            {
                var sellerNotify = new Notification
                {
                    UserId = order.VendorId,
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

            // 👉 bật cờ để View mở modal đánh giá
            TempData["ShowRatingModal"] = true;

            TempData["SuccessMessage"] = "Cảm ơn bạn! Đơn đã hoàn tất, kho đã trừ hàng và tiền đã thanh toán cho người bán.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ======================== GỬI ĐÁNH GIÁ SAU KHI NHẬN HÀNG ========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRating(
            int orderId,
            int rating,
            string? comment,
            [FromServices] EcommerceSecondHand.Services.BlockchainReputationService blockchainService
        )
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return NotFound();

            var order = await _orderRepository.GetOrderWithItemsAsync(orderId);
            if (order == null || order.UserId != currentUser.Id)
                return NotFound();

            if (order.Status != OrderStatus.Completed)
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể đánh giá sau khi đơn hàng đã hoàn tất.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            if (rating < 1) rating = 1;
            if (rating > 5) rating = 5;

            ApplicationUser? seller = null;
            if (!string.IsNullOrEmpty(order.SellerId))
            {
                seller = await _userManager.FindByIdAsync(order.SellerId);
            }

            if (seller == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người bán để đánh giá.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            // 👉 LƯU REVIEW VÀO DB CHO TỪNG SẢN PHẨM TRONG ĐƠN
            if (order.OrderItems != null)
            {
                foreach (var item in order.OrderItems)
                {
                    // tránh duplicate nếu user đã từng đánh giá sản phẩm này
                    var alreadyReviewed = await _reviewRepository.HasUserReviewedProductAsync(
                        currentUser.Id,
                        item.ProductId
                    );

                    if (!alreadyReviewed)
                    {
                        var review = new Review
                        {
                            ProductId = item.ProductId,
                            UserId = currentUser.Id,
                            Rating = rating,
                            Comment = comment,
                            DateCreated = DateTime.UtcNow,
                            IsApproved = false   // vẫn đi qua flow duyệt nếu bạn muốn
                        };

                        await _reviewRepository.AddAsync(review);
                    }
                }

                await _reviewRepository.SaveAsync();
            }

            // Ghi đánh giá lên "blockchain" (hoặc mock/offline)
            await blockchainService.RecordReviewAndSyncCacheAsync(seller, rating);

            TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá người bán! Uy tín on-chain và danh sách đánh giá sản phẩm đã được cập nhật.";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }
    }
}
