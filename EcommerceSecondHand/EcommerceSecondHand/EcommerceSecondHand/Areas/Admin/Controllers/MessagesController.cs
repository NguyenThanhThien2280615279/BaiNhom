using EcommerceSecondHand.Hubs;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EcommerceSecondHand.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MessagesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMessageRepository _messageRepository;
        private readonly IHubContext<ChatHub> _chatHub;
        private readonly INotificationRepository _notificationRepository;

        public MessagesController(
            UserManager<ApplicationUser> userManager,
            IMessageRepository messageRepository,
            IHubContext<ChatHub> chatHub,
            INotificationRepository notificationRepository)
        {
            _userManager = userManager;
            _messageRepository = messageRepository;
            _chatHub = chatHub;
            _notificationRepository = notificationRepository;
        }

        public async Task<IActionResult> Index(string? contactId = null)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return NotFound();
            }

            var contacts = await _messageRepository.GetUserContactsAsync(admin.Id);

            ApplicationUser? selectedContact = null;
            IEnumerable<Message> messages = new List<Message>();

            if (!string.IsNullOrEmpty(contactId))
            {
                selectedContact = await _userManager.FindByIdAsync(contactId);
                if (selectedContact != null)
                {
                    messages = await _messageRepository.GetConversationAsync(admin.Id, contactId);
                    await _messageRepository.MarkMessagesAsReadAsync(selectedContact.Id, admin.Id);
                }
            }
            else if (contacts.Any())
            {
                selectedContact = contacts.First();
                messages = await _messageRepository.GetConversationAsync(admin.Id, selectedContact.Id);
                await _messageRepository.MarkMessagesAsReadAsync(selectedContact.Id, admin.Id);
            }

            var model = new MessagesViewModel
            {
                Contacts = contacts,
                SelectedContact = selectedContact,
                Messages = messages,
                UserId = admin.Id,
                ContactId = selectedContact?.Id
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string recipientId, string content)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(recipientId) || string.IsNullOrEmpty(content))
            {
                return BadRequest("Người nhận và nội dung tin nhắn không được để trống.");
            }

            if (content.Length > 1000)
            {
                return BadRequest("Nội dung tin nhắn không được vượt quá 1000 ký tự.");
            }

            var recipient = await _userManager.FindByIdAsync(recipientId);
            if (recipient == null)
            {
                return NotFound("Không tìm thấy người nhận.");
            }

            var message = new Message
            {
                SenderId = admin.Id,
                ReceiverId = recipient.Id,
                Content = content,
                Timestamp = DateTime.UtcNow,
                DateSent = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            await _messageRepository.AddAsync(message);
            await _messageRepository.SaveAsync();

            await _chatHub.Clients.User(recipient.Id).SendAsync("ReceiveMessage", new
            {
                messageId = message.Id,
                senderId = admin.Id,
                senderName = "Admin",
                content = message.Content,
                timestamp = message.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                isRead = message.IsRead
            });

            var notification = new Notification
            {
                UserId = recipient.Id,
                Title = "Tin nhắn từ Admin",
                Message = content.Length > 50 ? content.Substring(0, 47) + "..." : content,
                Type = NotificationType.Message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = admin.Id,
                ActionUrl = $"/Messages?contactId={admin.Id}"
            };

            await _notificationRepository.AddAsync(notification);
            await _notificationRepository.SaveAsync();

            return RedirectToAction(nameof(Index), new { contactId = recipientId });
        }

        [HttpGet]
        public async Task<IActionResult> GetConversation(string contactId)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(contactId))
            {
                return BadRequest("Không có người liên hệ được chọn.");
            }

            var contact = await _userManager.FindByIdAsync(contactId);
            if (contact == null)
            {
                return NotFound("Không tìm thấy người liên hệ.");
            }

            var messages = await _messageRepository.GetConversationAsync(admin.Id, contactId);
            await _messageRepository.MarkMessagesAsReadAsync(contactId, admin.Id);

            var model = new ConversationViewModel
            {
                Contact = contact,
                Messages = messages,
                UserId = admin.Id,
                ContactId = contactId,
                IsAdminContact = false
            };

            return PartialView("_ConversationPartial", model);
        }
    }
}
