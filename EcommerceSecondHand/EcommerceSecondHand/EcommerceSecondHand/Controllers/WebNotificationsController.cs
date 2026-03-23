using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories.Interfaces;
using EcommerceSecondHand.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSecondHand.Controllers
{
    [Authorize]
    [DisallowRole("Admin")]
    public class WebNotificationsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationRepository _notificationRepository;

        public WebNotificationsController(
            UserManager<ApplicationUser> userManager,
            INotificationRepository notificationRepository)
        {
            _userManager = userManager;
            _notificationRepository = notificationRepository;
        }

        public async Task<IActionResult> Index(string? type = null, bool unreadOnly = false, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            const int pageSize = 15;
            
            // L?y th�ng b�o d?a tr�n b? l?c
            NotificationType? notificationType = null;
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<NotificationType>(type, out var parsedType))
            {
                notificationType = parsedType;
            }

            var notifications = await _notificationRepository.GetPaginatedNotificationsAsync(
                user.Id, notificationType, unreadOnly, page, pageSize);
            
            var totalCount = await _notificationRepository.GetNotificationsCountAsync(
                user.Id, notificationType, unreadOnly);

            // ??m s? th�ng b�o ch?a ??c
            var unreadCount = await _notificationRepository.GetUnreadNotificationsCountAsync(user.Id);

            var model = new NotificationsViewModel
            {
                Notifications = notifications,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                UnreadCount = unreadCount,
                SelectedType = notificationType,
                UnreadOnly = unreadOnly
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null || notification.UserId != user.Id)
            {
                return NotFound();
            }

            notification.IsRead = true;
            await _notificationRepository.UpdateAsync(notification);
            await _notificationRepository.SaveAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            await _notificationRepository.MarkAllAsReadAsync(user.Id);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var notification = await _notificationRepository.GetByIdAsync(id);
            if (notification == null || notification.UserId != user.Id)
            {
                return NotFound();
            }

            await _notificationRepository.DeleteAsync(id);
            await _notificationRepository.SaveAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAll()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            await _notificationRepository.DeleteAllAsync(user.Id);

            return RedirectToAction(nameof(Index));
        }
    }
}