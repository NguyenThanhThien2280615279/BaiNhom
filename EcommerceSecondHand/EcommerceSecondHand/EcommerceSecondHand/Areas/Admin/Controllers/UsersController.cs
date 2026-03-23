using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceSecondHand.Data;

namespace EcommerceSecondHand.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IReviewRepository _reviewRepository;
        private readonly IPaymentTransactionRepository _transactionRepository;
        private readonly ApplicationDbContext _dbContext;
        private const string ErrorKey = "ErrorMessage";
        private const string SuccessKey = "SuccessMessage";

        public UsersController(
            UserManager<ApplicationUser> userManager, 
            RoleManager<IdentityRole> roleManager,
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            IReviewRepository reviewRepository,
            IPaymentTransactionRepository transactionRepository,
            ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _reviewRepository = reviewRepository;
            _transactionRepository = transactionRepository;
            _dbContext = dbContext;
        }

        // GET: Admin/Users
        [HttpGet]
        public async Task<IActionResult> Index(string? role = null, string? search = null, int page = 1, int pageSize = 20)
        {
            var query = _userManager.Users.AsQueryable();

            // Filter by search
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => u.Email!.Contains(search) || 
                                        u.FirstName.Contains(search) || 
                                        u.LastName.Contains(search));
            }

            // Filter by role
            if (!string.IsNullOrWhiteSpace(role))
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                var userIds = usersInRole.Select(u => u.Id);
                query = query.Where(u => userIds.Contains(u.Id));
            }

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get user roles for each user
            var usersWithRoles = new List<dynamic>();
            foreach (var user in users)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                usersWithRoles.Add(new { User = user, Roles = userRoles });
            }

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            ViewBag.CurrentRole = role;
            ViewBag.Search = search;
            ViewBag.UsersWithRoles = usersWithRoles;
            return View(users);
        }


        // GET: Admin/Users/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var orders = await _orderRepository.GetUserOrdersAsync(id);
            var products = await _productRepository.GetProductsBySellerAsync(id, "newest", 1, 10);
            var reviews = await _reviewRepository.GetRecentReviewsByUserAsync(id, 10);
            var transactions = await _transactionRepository.GetTransactionsByUserAsync(id, null, null, null, 1, 10);

            var model = new AdminUserDetailsViewModel
            {
                User = user,
                Roles = userRoles,
                Orders = orders,
                Products = products,
                Reviews = reviews,
                Transactions = transactions
            };

            return View(model);
        }

        // GET: Admin/Users/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

            var model = new AdminEditUserViewModel
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                City = user.City,
                Country = user.Country,
                PostalCode = user.PostalCode,
                Bio = user.Bio,
                WalletBalance = user.WalletBalance,
                EmailConfirmed = user.EmailConfirmed,
                LockoutEnabled = user.LockoutEnabled,
                LockoutEnd = user.LockoutEnd,
                CurrentRole = userRoles.FirstOrDefault(),
                AvailableRoles = allRoles
            };

            return View(model);
        }

        // POST: Admin/Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminEditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                TempData[ErrorKey] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Index));
            }

            // Update user properties
            user.Email = model.Email;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;
            user.City = model.City;
            user.Country = model.Country;
            user.PostalCode = model.PostalCode;
            user.Bio = model.Bio;
            user.WalletBalance = model.WalletBalance;
            user.EmailConfirmed = model.EmailConfirmed;
            user.LockoutEnabled = model.LockoutEnabled;
            user.LockoutEnd = model.LockoutEnd;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                model.AvailableRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
                return View(model);
            }

            // Update role if changed
            if (!string.IsNullOrEmpty(model.CurrentRole))
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (!currentRoles.Contains(model.CurrentRole))
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, model.CurrentRole);
                }
            }

            TempData[SuccessKey] = "Đã cập nhật thông tin người dùng thành công.";
            return RedirectToAction(nameof(Details), new { id = user.Id });
        }

    

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData[ErrorKey] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Index));
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow;
            }
            else
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData[ErrorKey] = "Không thể cập nhật khoá tài khoản.";
                return RedirectToAction(nameof(Index));
            }
            TempData[SuccessKey] = "Đã cập nhật trạng thái khoá tài khoản.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRole(string id, string role)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData[ErrorKey] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Index));
            }
            if (!await _roleManager.RoleExistsAsync(role))
            {
                TempData[ErrorKey] = "Vai trò không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var remove = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            var add = await _userManager.AddToRoleAsync(user, role);
            if (!remove.Succeeded || !add.Succeeded)
            {
                TempData[ErrorKey] = "Không thể cập nhật vai trò người dùng.";
                return RedirectToAction(nameof(Index));
            }
            
            TempData[SuccessKey] = "Đã cập nhật vai trò người dùng.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreditWallet(string id, decimal amount, string? description)
        {
            if (amount <= 0)
            {
                TempData[ErrorKey] = "Số tiền phải lớn hơn 0.";
                return RedirectToAction(nameof(Index));
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData[ErrorKey] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(Index));
            }

            user.WalletBalance += amount;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData[ErrorKey] = "Không thể cộng tiền vào ví.";
                return RedirectToAction(nameof(Index));
            }
            
            TempData[SuccessKey] = $"Đã cộng {amount:N0} đ vào ví.";
            return RedirectToAction(nameof(Index));
        }
    }
}


