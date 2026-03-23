using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories.Interfaces;
using EcommerceSecondHand.Services;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSecondHand.Controllers
{
    [Authorize]
    public class RechargeController : Controller
    {
        private readonly IRechargePackageRepository _packageRepository;
        private readonly IRechargeTransactionRepository _transactionRepository;
        private readonly IPaymentTransactionRepository _paymentTransactionRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly VnPayService _vnPayService;

        public RechargeController(
            IRechargePackageRepository packageRepository,
            IRechargeTransactionRepository transactionRepository,
            IPaymentTransactionRepository paymentTransactionRepository,
            UserManager<ApplicationUser> userManager,
            VnPayService vnPayService)
        {
            _packageRepository = packageRepository;
            _transactionRepository = transactionRepository;
            _paymentTransactionRepository = paymentTransactionRepository;
            _userManager = userManager;
            _vnPayService = vnPayService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var packages = await _packageRepository.GetActivePackagesAsync();
            var recentTransactions = await _transactionRepository.GetUserTransactionsAsync(user.Id, 1, 5);

            var model = new RechargeViewModel
            {
                Packages = packages,
                CurrentBalance = user.WalletBalance,
                RecentTransactions = recentTransactions
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Withdraw()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            ViewBag.CurrentBalance = user.WalletBalance;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(decimal amount, string? description)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Index));
            }


            if (amount <= 0)
            {
                TempData["Error"] = "Số tiền rút phải lớn hơn 0.";
                return RedirectToAction(nameof(Withdraw));
            }

            if (amount < 1000)
            {
                TempData["Error"] = "Số tiền rút tối thiểu là 1.000 đ.";
                return RedirectToAction(nameof(Withdraw));
            }
            if (amount > user.WalletBalance)
            {
                TempData["Error"] = "Số dư ví không đủ để thực hiện rút tiền.";
                return RedirectToAction(nameof(Withdraw));
            }

            var transaction = new PaymentTransaction
            {
                UserId = user.Id,
                Amount = amount,
                Type = TransactionType.Withdraw,
                Status = TransactionStatus.Completed,
                Description = string.IsNullOrWhiteSpace(description) ? "Rút tiền ví" : description,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                TransactionReference = $"WD-{Guid.NewGuid():N}".ToUpper()
            };

            user.WalletBalance -= amount;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["Error"] = "Không thể cập nhật số dư ví.";
                return RedirectToAction(nameof(Withdraw));
            }

            await _paymentTransactionRepository.AddAsync(transaction);
            await _paymentTransactionRepository.SaveAsync();

            TempData["Success"] = $"Đã rút {amount:N0} đ thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CreateTransaction(int packageId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Người dùng không tồn tại" });

            try
            {
                var package = await _packageRepository.GetByIdAsync(packageId);
                if (package == null)
                    return Json(new { success = false, message = "Gói nạp tiền không tồn tại" });

                var transaction = await _transactionRepository.CreateTransactionAsync(user.Id, packageId);
                
                // Tạo PaymentInformationModel cho VNPay mới
                var paymentModel = new PaymentInformationModel
                {
                    OrderType = "other",
                    Amount = (double)transaction.Amount,
                    OrderDescription = $"Nạp tiền {package.Name}",
                    Name = $"{user.FirstName} {user.LastName}",
                    OrderId = transaction.TransactionCode // Sử dụng TransactionCode làm OrderId
                };

                // Redirect đến action CreatePaymentUrlVnpay
                return Json(new { 
                    success = true, 
                    redirectUrl = Url.Action("CreatePaymentUrlVnpay", "Recharge", paymentModel),
                    transactionCode = transaction.TransactionCode,
                    amount = transaction.Amount,
                    totalAmount = transaction.TotalAmount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        public async Task<IActionResult> PaymentReturn()
        {
            // Sử dụng VNPay mới để xử lý response
            var response = _vnPayService.PaymentExecute(Request.Query);
            
            if (!response.Success)
            {
                TempData["Error"] = "Dữ liệu thanh toán không hợp lệ";
                return RedirectToAction("Index");
            }

            // Lấy transaction từ OrderId (vnp_TxnRef)
            var transaction = await _transactionRepository.GetByTransactionCodeAsync(response.OrderId);
            if (transaction == null)
            {
                TempData["Error"] = "Giao dịch không tồn tại";
                return RedirectToAction("Index");
            }

            if (response.VnPayResponseCode == "00") // Thành công
            {
                var success = await _transactionRepository.CompleteTransactionAsync(
                    transaction.Id, 
                    response.TransactionId, 
                    response.VnPayResponseCode, 
                    response.OrderDescription
                );

                if (success)
                {
                    TempData["Success"] = $"Nạp tiền thành công! Số dư ví đã được cập nhật (+{transaction.TotalAmount:N0}₫)";
                }
                else
                {
                    TempData["Error"] = "Có lỗi xảy ra khi cập nhật số dư ví";
                }
            }
            else
            {
                await _transactionRepository.CancelTransactionAsync(transaction.Id);
                TempData["Error"] = "Thanh toán thất bại";
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> TransactionHistory(int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            const int pageSize = 10;
            var transactions = await _transactionRepository.GetUserTransactionsAsync(user.Id, page, pageSize);
            var totalCount = await _transactionRepository.CountUserTransactionsAsync(user.Id);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return View(transactions);
        }

        [HttpPost]
        public async Task<IActionResult> CancelTransaction(int transactionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Người dùng không tồn tại" });

            var transaction = await _transactionRepository.GetByIdAsync(transactionId);
            if (transaction == null || transaction.UserId != user.Id)
                return Json(new { success = false, message = "Giao dịch không tồn tại" });

            var success = await _transactionRepository.CancelTransactionAsync(transactionId);
            return Json(new { success = success, message = success ? "Đã hủy giao dịch" : "Không thể hủy giao dịch này" });
        }

        [HttpPost]
        public IActionResult CreatePaymentUrlVnpay(PaymentInformationModel model)
        {
            var url = _vnPayService.CreatePaymentUrl(model, HttpContext);

            return Redirect(url);
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallbackVnpay()
        {
            try
            {
                var response = _vnPayService.PaymentExecute(Request.Query);
                
                if (!response.Success)
                {
                    TempData["Error"] = "Dữ liệu thanh toán không hợp lệ";
                    return RedirectToAction("Index");
                }

                // Lấy transaction từ OrderId (vnp_TxnRef)
                var transaction = await _transactionRepository.GetByTransactionCodeAsync(response.OrderId);
                if (transaction == null)
                {
                    TempData["Error"] = "Giao dịch không tồn tại";
                    return RedirectToAction("Index");
                }

                // Xử lý response code với VnPayResponseService
                var responseResult = VnPayResponseService.ProcessResponse(
                    response.VnPayResponseCode, 
                    response.OrderId, 
                    transaction.TotalAmount
                );

                if (responseResult.ShouldUpdateWallet)
                {
                    // Cập nhật ví và hoàn thành giao dịch
                    var success = await _transactionRepository.CompleteTransactionAsync(
                        transaction.Id, 
                        response.TransactionId, 
                        response.VnPayResponseCode, 
                        response.OrderDescription
                    );

                    if (success)
                    {
                        if (responseResult.IsSuspicious)
                        {
                            TempData["Warning"] = $"{responseResult.Message} - Số dư ví đã được cập nhật (+{transaction.TotalAmount:N0}₫)";
                        }
                        else
                        {
                            TempData["Success"] = $"{responseResult.Message} - Số dư ví đã được cập nhật (+{transaction.TotalAmount:N0}₫)";
                        }
                    }
                    else
                    {
                        TempData["Error"] = "Có lỗi xảy ra khi cập nhật số dư ví";
                    }
                }
                else
                {
                    // Hủy giao dịch và hiển thị thông báo lỗi chi tiết
                    await _transactionRepository.CancelTransactionAsync(transaction.Id);
                    TempData["Error"] = $"{responseResult.Message}";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi xử lý thanh toán: " + ex.Message;
            return RedirectToAction("Index");
        }
    }
}
}
