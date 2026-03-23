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
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationRepository _notificationRepository;

        public NotificationsController(
            UserManager<ApplicationUser> userManager,
            INotificationRepository notificationRepository)
        {
            _userManager = userManager;
            _notificationRepository = notificationRepository;
        }

        [HttpGet("GetUnreadCount")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var count = await _notificationRepository.GetUnreadNotificationsCountAsync(user.Id);
            return Ok(count);
        }

        [HttpGet("GetRecent")]
        public async Task<IActionResult> GetRecent(int count = 5)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var notifications = await _notificationRepository.GetRecentNotificationsAsync(user.Id, count);
            var result = notifications.Select(n => new
            {
                id = n.Id,
                title = n.Title,
                message = n.Message,
                type = n.Type,
                isRead = n.IsRead,
                createdAt = n.CreatedAt,
                actionUrl = n.ActionUrl
            });
            return Ok(result);
        }

        [HttpPost("MarkAsRead/{id}")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null || notification.UserId != user.Id)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _notificationRepository.UpdateAsync(notification);
            await _notificationRepository.SaveAsync();

            return Ok(new { success = true });
        }

        [HttpPost("MarkAllAsRead")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            await _notificationRepository.MarkAllAsReadAsync(user.Id);

            return Ok(new { success = true });
        }
    }
}