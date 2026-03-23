using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EcommerceSecondHand.Data;
using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Controllers
{
    [Authorize]
    public class OnChainController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public OnChainController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager
        )
        {
            _db = db;
            _userManager = userManager;
        }

        // /OnChain/Index?address=0x...
        public async Task<IActionResult> Index(string? address)
        {
            // Nếu không truyền address -> lấy địa chỉ của user hiện tại (nếu họ là seller)
            if (string.IsNullOrWhiteSpace(address))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null && !string.IsNullOrWhiteSpace(user.BlockchainAddress))
                {
                    address = user.BlockchainAddress;
                }
            }

            ViewBag.IsDemoMode = true; // mock mode
            ViewBag.Address = address ?? "(Không có address)";

            var logsQuery = _db.OnChainLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(address))
            {
                logsQuery = logsQuery.Where(x => x.Address == address);
            }

            var logs = logsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Take(200)
                .ToList();

            return View(logs);
        }
    }
}
