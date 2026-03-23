using System.Text.RegularExpressions;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSecondHand.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISystemStatisticsRepository _statisticsRepository;
        private readonly IProductRepository _productRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IPaymentTransactionRepository _transactionRepository;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            ISystemStatisticsRepository statisticsRepository,
            IProductRepository productRepository,
            IOrderRepository orderRepository,
            IPaymentTransactionRepository transactionRepository)
        {
            _userManager = userManager;
            _statisticsRepository = statisticsRepository;
            _productRepository = productRepository;
            _orderRepository = orderRepository;
            _transactionRepository = transactionRepository;
        }

        // ===================== DASHBOARD =====================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new AdminDashboardViewModel
            {
                Statistics = await _statisticsRepository.GetStatisticsAsync(),
                RecentUsers = await _userManager.Users.OrderByDescending(u => u.CreatedAt).Take(5).ToListAsync(),
                RecentProducts = await _productRepository.GetRecentProductsAsync(5),
                RecentOrders = await _orderRepository.GetRecentOrdersAsync(5),
                RecentTransactions = await _transactionRepository.GetRecentTransactionsAsync(5),
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalProducts = await _productRepository.CountProductsAsync(),
                TotalOrders = await _orderRepository.CountOrdersAsync(),
                TotalRevenue = await _orderRepository.GetTotalRevenueAsync(),
                PendingVerifications = await _userManager.Users.CountAsync(u => !u.EmailConfirmed),
                ReportedItems = await _productRepository.CountReportedProductsAsync()
            };

            return View(model);
        }

        // ===================== WALLET MANAGEMENT =====================
        [HttpGet]
        public async Task<IActionResult> WalletManagement(
            string? filterType = null,
            string? filterStatus = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int page = 1)
        {
            const int pageSize = 20;

            // Bảng bên dưới theo bộ lọc
            var recentTransactions = await _transactionRepository.GetFilteredTransactionsAsync(
                null, filterType, filterStatus, startDate, endDate, page, pageSize);

            var totalCount = await _transactionRepository.CountFilteredTransactionsAsync(
                filterType, filterStatus, startDate, endDate);

            // Đang chờ xử lý (thẻ KPIs)
            var pendingTransactions = await _transactionRepository.GetPendingTransactionsAsync();

            // Ví hệ thống = số dư admin hiện tại (KHÔNG cộng hoa hồng)
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var adminUser = admins.FirstOrDefault();

            // Tổng hoa hồng admin MỌI THỜI GIAN
            var allCompleted = await _transactionRepository.GetTransactionsByStatusAsync(TransactionStatus.Completed);
            var commissionAllTime = allCompleted
                .Where(t => t.Type == TransactionType.AdminAdjustment
                         && (t.Description ?? "").Contains("Hoa hồng"))
                .Sum(t => t.Amount);

            // Map hiển thị nút hoàn tiền (escrow của đơn đã Canceled và chưa có Refund theo OrderId)
            var refundableMap = new Dictionary<int, bool>();
            foreach (var tx in recentTransactions)
            {
                bool allow = false;
                if (tx.Type == TransactionType.AdminAdjustment && tx.OrderId != null && tx.Order != null)
                {
                    if (tx.Order.Status == OrderStatus.Canceled)
                    {
                        var buyerId = tx.Order.UserId;
                        var refunds = await _transactionRepository.GetFilteredTransactionsAsync(
                            buyerId, nameof(TransactionType.Refund), null, null, null, 1, 50);
                        allow = !refunds.Any(r => r.OrderId == tx.OrderId);
                    }
                }
                refundableMap[tx.Id] = allow;
            }
            ViewBag.RefundableMap = refundableMap;

            var model = new WalletManagementViewModel
            {
                PendingTransactions = pendingTransactions,
                RecentTransactions = recentTransactions,

                TotalPendingDeposits = pendingTransactions
                    .Where(t => t.Type == TransactionType.Deposit)
                    .Sum(t => t.Amount),

                TotalPendingWithdrawals = pendingTransactions
                    .Where(t => t.Type == TransactionType.Withdraw || t.Type == TransactionType.Withdrawal)
                    .Sum(t => t.Amount),

                // KHÔNG cộng hoa hồng vào số dư
                TotalSystemBalance = adminUser?.WalletBalance ?? 0m,

                // Hoa hồng tổng mọi thời gian
                TotalCommissionEarned = commissionAllTime,

                CommissionRate = 0.10m,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                FilterType = filterType,
                FilterStatus = filterStatus,
                StartDate = startDate,
                EndDate = endDate
            };

            return View(model);
        }

        // ===================== APPROVE =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTransaction(int id)
        {
            var tx = await _transactionRepository.GetByIdAsync(id);
            if (tx == null) return NotFound();

            tx.Status = TransactionStatus.Completed;
            tx.ProcessedAt = DateTime.UtcNow;
            await _transactionRepository.UpdateAsync(tx);

            var user = await _userManager.FindByIdAsync(tx.UserId);
            if (user != null)
            {
                if (tx.Type == TransactionType.Deposit)
                    user.WalletBalance += tx.Amount;
                else if (tx.Type == TransactionType.Withdraw || tx.Type == TransactionType.Withdrawal)
                    user.WalletBalance -= tx.Amount;

                await _userManager.UpdateAsync(user);
            }

            await _transactionRepository.SaveAsync();
            TempData["SuccessMessage"] = "✅ Đã duyệt giao dịch.";
            return RedirectToAction(nameof(WalletManagement));
        }

        // ===================== REJECT =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTransaction(int id)
        {
            var tx = await _transactionRepository.GetByIdAsync(id);
            if (tx == null) return NotFound();

            tx.Status = TransactionStatus.Rejected;
            tx.ProcessedAt = DateTime.UtcNow;
            await _transactionRepository.UpdateAsync(tx);
            await _transactionRepository.SaveAsync();

            TempData["ErrorMessage"] = "❌ Đã từ chối giao dịch.";
            return RedirectToAction(nameof(WalletManagement));
        }

        // ===================== API CHI TIẾT GIAO DỊCH =====================
        [HttpGet]
        public async Task<IActionResult> GetTransactionDetails(int id)
        {
            var tx = await _transactionRepository.GetByIdAsync(id);
            if (tx == null) return NotFound();

            tx.User ??= await _userManager.FindByIdAsync(tx.UserId);

            return Json(new
            {
                id = tx.Id,
                type = tx.Type.ToString(),
                status = tx.Status.ToString(),
                amount = tx.Amount.ToString("N0"),
                description = tx.Description,
                createdAt = tx.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                userName = tx.User?.UserName,
                email = tx.User?.Email,
                orderId = tx.OrderId
            });
        }

        // ===================== REFUND (ĐƠN ADMIN HỦY) =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefundEscrow(int transactionId, int orderId)
        {
            var escrowTx = await _transactionRepository.GetByIdAsync(transactionId);
            var order = await _orderRepository.GetByIdAsync(orderId);

            if (escrowTx == null || order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy giao dịch/đơn hàng.";
                return RedirectToAction(nameof(WalletManagement));
            }

            if (escrowTx.Type != TransactionType.AdminAdjustment || escrowTx.OrderId != order.Id)
            {
                TempData["ErrorMessage"] = "Giao dịch không phải escrow của đơn.";
                return RedirectToAction(nameof(WalletManagement));
            }

            if (order.Status != OrderStatus.Canceled)
            {
                TempData["ErrorMessage"] = "Đơn chưa hủy nên không thể hoàn tiền.";
                return RedirectToAction(nameof(WalletManagement));
            }

            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var adminUser = admins.FirstOrDefault();
            if (adminUser == null)
            {
                TempData["ErrorMessage"] = "Không có ví hệ thống.";
                return RedirectToAction(nameof(WalletManagement));
            }

            var buyer = await _userManager.FindByIdAsync(order.UserId);
            if (buyer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người mua.";
                return RedirectToAction(nameof(WalletManagement));
            }

            // chống hoàn trùng theo OrderId
            var existedRefunds = await _transactionRepository.GetFilteredTransactionsAsync(
                buyer.Id, nameof(TransactionType.Refund), null, null, null, 1, 50);
            if (existedRefunds.Any(x => x.OrderId == order.Id))
            {
                TempData["ErrorMessage"] = "Đơn đã được hoàn trước đó.";
                return RedirectToAction(nameof(WalletManagement));
            }

            var amount = escrowTx.Amount;
            if (adminUser.WalletBalance < amount)
            {
                TempData["ErrorMessage"] = "Ví hệ thống không đủ để hoàn.";
                return RedirectToAction(nameof(WalletManagement));
            }

            try
            {
                adminUser.WalletBalance -= amount;
                buyer.WalletBalance += amount;
                await _userManager.UpdateAsync(adminUser);
                await _userManager.UpdateAsync(buyer);

                var orderNo = string.IsNullOrWhiteSpace(order.OrderNumber) ? order.Id.ToString() : order.OrderNumber;

                await _transactionRepository.AddAsync(new PaymentTransaction
                {
                    Type = TransactionType.Refund,
                    Status = TransactionStatus.Completed,
                    Amount = amount,
                    UserId = buyer.Id,
                    OrderId = order.Id,
                    Description = $"Hoàn tiền đơn #{orderNo} (Admin hủy) – từ escrow tx #{escrowTx.Id}",
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow,
                    TransactionReference = $"REFUND-ORD-{orderNo}",
                    PaymentMethod = "Wallet"
                });

                order.Status = OrderStatus.Refunded;
                order.UpdatedAt = DateTime.UtcNow;
                await _orderRepository.UpdateAsync(order);

                await _transactionRepository.SaveAsync();
                TempData["SuccessMessage"] = "✅ Hoàn tiền thành công!";
            }
            catch
            {
                TempData["ErrorMessage"] = "Hoàn tiền thất bại.";
            }

            return RedirectToAction(nameof(WalletManagement));
        }

        // helper (nếu cần)
        private static string? ExtractOrderNumber(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = Regex.Match(text, @"#(?<code>[A-Za-z0-9\-]+)");
            return m.Success ? m.Groups["code"].Value : null;
        }
        // ===================== ONE-OFF: CHUẨN HOÁ VÍ ADMIN =====================
        // Trừ toàn bộ (hoặc đến một ngày nhất định) số hoa hồng đã ghi nhận trước đây ra khỏi WalletBalance.
        // Gợi ý: chạy một lần, sau đó KHÔNG cần chạy lại.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NormalizeSystemWallet(DateTime? until = null, bool dryRun = false)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var admin = admins.FirstOrDefault();
            if (admin == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ví hệ thống (Admin).";
                return RedirectToAction(nameof(WalletManagement));
            }

            // Lấy tất cả giao dịch hoa hồng Completed (mọi thời gian hoặc tới 'until')
            var commissions = await _transactionRepository.GetFilteredTransactionsAsync(
                userId: null,
                type: nameof(TransactionType.AdminAdjustment),
                status: nameof(TransactionStatus.Completed),
                startDate: null,
                endDate: until,
                page: 1,
                pageSize: int.MaxValue
            );

            // Chỉ tính những tx mô tả có "Hoa hồng"
            var commissionTotal = commissions
                .Where(t => (t.Description ?? "").Contains("Hoa hồng", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);

            if (commissionTotal <= 0)
            {
                TempData["SuccessMessage"] = "Ví hệ thống không cần chuẩn hoá.";
                return RedirectToAction(nameof(WalletManagement));
            }

            var current = admin.WalletBalance;
            var delta = Math.Min(current, commissionTotal); // chỉ trừ tối đa bằng số dư hiện tại
            if (delta <= 0)
            {
                TempData["SuccessMessage"] = "Số dư hiện tại = 0, không thể trừ thêm.";
                return RedirectToAction(nameof(WalletManagement));
            }

            if (dryRun)
            {
                TempData["SuccessMessage"] = $"(Dry-run) Sẽ trừ {delta:N0} đ khỏi ví admin (tổng hoa hồng: {commissionTotal:N0} đ, số dư hiện tại: {current:N0} đ).";
                return RedirectToAction(nameof(WalletManagement));
            }

            // Thực hiện trong 1 transaction DB cho chắc (nếu repo của bạn có hỗ trợ thì bọc lại)
            admin.WalletBalance -= delta;
            await _userManager.UpdateAsync(admin);

            // Tuỳ schema có cho phép số âm ở Amount hay không.
            // Nếu KHÔNG cho số âm, bỏ qua ghi tx âm, chỉ log mô tả.
            // Nếu CHO phép, bạn có thể mở comment dưới.
            /*
            await _transactionRepository.AddAsync(new PaymentTransaction
            {
                UserId = admin.Id,
                Amount = -delta,
                Type = TransactionType.AdminAdjustment,
                Status = TransactionStatus.Completed,
                Description = "Điều chỉnh chuẩn hoá ví admin: loại phần hoa hồng đã lỡ cộng",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                PaymentMethod = "Wallet"
            });
            */

            await _transactionRepository.SaveAsync();

            var leftover = commissionTotal - delta; // phần hoa hồng còn lại vốn dĩ chưa hề nằm trong ví
            TempData["SuccessMessage"] =
                $"✅ Đã chuẩn hoá ví hệ thống: trừ {delta:N0} đ. " +
                (leftover > 0 ? $"(Phần hoa hồng còn lại {leftover:N0} đ chỉ là thống kê, không nằm trong ví)" : "");

            return RedirectToAction(nameof(WalletManagement));
        }

    }
}
