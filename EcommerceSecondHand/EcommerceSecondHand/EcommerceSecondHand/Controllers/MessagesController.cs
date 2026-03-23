using EcommerceSecondHand.Hubs;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Models.ViewModels;
using EcommerceSecondHand.Repositories.Interfaces;
using EcommerceSecondHand.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EcommerceSecondHand.Controllers
{
    [Authorize]
    [DisallowRole("Admin")]
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
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Lấy danh sách người liên hệ
            var contacts = await _messageRepository.GetUserContactsAsync(user.Id);

            // Kiểm tra xem admin đã có trong danh sách liên hệ chưa
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var admin = adminUsers.FirstOrDefault();
            ViewBag.HasAdminContact = admin != null && contacts.Any(c => c.Id == admin.Id);

            ApplicationUser? selectedContact = null;
            IEnumerable<Message> messages = new List<Message>();

            if (!string.IsNullOrEmpty(contactId))
            {
                // Lấy thông tin người liên hệ đã chọn
                selectedContact = await _userManager.FindByIdAsync(contactId);
                if (selectedContact != null)
                {
                    // Lấy tin nhắn giữa người dùng và người liên hệ
                    messages = await _messageRepository.GetConversationAsync(user.Id, contactId);

                    // Đánh dấu tin nhắn đã đọc
                    await _messageRepository.MarkMessagesAsReadAsync(user.Id, contactId);
                }
            }
            else if (contacts.Any())
            {
                // Nếu không có contactId, lấy người liên hệ đầu tiên
                selectedContact = contacts.First();
                messages = await _messageRepository.GetConversationAsync(user.Id, selectedContact.Id);
                await _messageRepository.MarkMessagesAsReadAsync(user.Id, selectedContact.Id);
            }

            var model = new MessagesViewModel
            {
                Contacts = contacts,
                SelectedContact = selectedContact,
                Messages = messages,
                UserId = user.Id,
                ContactId = selectedContact?.Id
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string recipientId, string content)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
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

            // Prevent sending messages to self
            if (recipientId == user.Id)
            {
                TempData["ErrorMessage"] = "Bạn không thể nhắn tin cho chính mình.";
                return RedirectToAction(nameof(Index));
            }

            var recipient = await _userManager.FindByIdAsync(recipientId);
            if (recipient == null)
            {
                return NotFound("Không tìm thấy người nhận.");
            }

            // Tạo tin nhắn mới
            var message = new Message
            {
                SenderId = user.Id,
                RecipientId = recipientId,
                Content = content,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            };

            await _messageRepository.AddAsync(message);
            await _messageRepository.SaveAsync();
            
            // Log để debug
            Console.WriteLine($"Message saved: ID={message.Id}, Sender={message.SenderId}, Recipient={message.RecipientId}, Content={message.Content}");

            // Gửi thông báo qua SignalR
            await _chatHub.Clients.User(recipientId).SendAsync("ReceiveMessage", new
            {
                messageId = message.Id,
                senderId = message.SenderId,
                senderName = $"{user.FirstName} {user.LastName}",
                content = message.Content,
                timestamp = message.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                isRead = message.IsRead
            });

            // Tạo thông báo cho người nhận
            var notification = new Notification
            {
                UserId = recipientId,
                Title = $"Tin nhắn mới từ {user.FirstName} {user.LastName}",
                Message = content.Length > 50 ? content.Substring(0, 47) + "..." : content,
                Type = NotificationType.Message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = user.Id,
                ActionUrl = $"/Messages?contactId={user.Id}"
            };

            await _notificationRepository.AddAsync(notification);
            await _notificationRepository.SaveAsync();

            return RedirectToAction(nameof(Index), new { contactId = recipientId });
            }
            catch (Exception ex)
            {
                return BadRequest($"Lỗi khi gửi tin nhắn: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetConversation(string contactId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(contactId))
            {
                return BadRequest("Kh�ng c� ng??i li�n h? ???c ch?n.");
            }

            var contact = await _userManager.FindByIdAsync(contactId);
            if (contact == null)
            {
                return NotFound("Kh�ng t�m th?y ng??i li�n h?.");
            }

            var messages = await _messageRepository.GetConversationAsync(user.Id, contactId);
            await _messageRepository.MarkMessagesAsReadAsync(user.Id, contactId);

            var model = new ConversationViewModel
            {
                Contact = contact,
                Messages = messages,
                UserId = user.Id,
                ContactId = contactId,
                IsAdminContact = false
            };

            return PartialView("_ConversationPartial", model);
        }

        // Chức năng chat với admin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessageToAdmin(string content)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                if (string.IsNullOrEmpty(content))
                {
                    return BadRequest("Nội dung tin nhắn không được để trống.");
                }

                if (content.Length > 1000)
                {
                    return BadRequest("Nội dung tin nhắn không được vượt quá 1000 ký tự.");
                }

                // Tìm admin đầu tiên
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                Console.WriteLine($"Found {adminUsers.Count()} admin users");
                if (!adminUsers.Any())
                {
                    Console.WriteLine("No admin users found in system");
                    return BadRequest("Không có admin nào trong hệ thống.");
                }

                var admin = adminUsers[0];
                Console.WriteLine($"Using admin: {admin.UserName} (ID: {admin.Id})");

                // Tạo tin nhắn mới
                var message = new Message
                {
                    SenderId = user.Id,
                    RecipientId = admin.Id,
                    Content = content,
                    Timestamp = DateTime.UtcNow,
                    DateSent = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                await _messageRepository.AddAsync(message);
                await _messageRepository.SaveAsync();
                
                // Log để debug
                Console.WriteLine($"Message to admin saved: ID={message.Id}, Sender={message.SenderId}, Recipient={message.RecipientId}, Content={message.Content}");

                // Gửi thông báo qua SignalR (sử dụng Clients.All vì admin có thể không có trong _userConnections)
                Console.WriteLine($"Sending SignalR message to all clients (admin should receive)");
                await _chatHub.Clients.All.SendAsync("ReceiveMessage", new
                {
                    messageId = message.Id,
                    senderId = message.SenderId,
                    senderName = $"{user.FirstName} {user.LastName}",
                    content = message.Content,
                    timestamp = message.Timestamp.ToString("HH:mm"),
                    isRead = message.IsRead
                });
                Console.WriteLine("SignalR message sent successfully");

                // Tạo thông báo cho admin
                var notification = new Notification
                {
                    UserId = admin.Id,
                    Title = "Tin nhắn mới từ khách hàng",
                    Message = $"Bạn có tin nhắn mới từ {user.FirstName} {user.LastName} ({user.UserName})",
                    Type = NotificationType.Message,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedEntityId = message.Id.ToString(),
                    ActionUrl = $"/Admin/Messages?contactId={user.Id}"
                };

                await _notificationRepository.AddAsync(notification);
                await _notificationRepository.SaveAsync();

                return Json(new { success = true, messageId = message.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Test endpoint để kiểm tra admin
        [HttpGet]
        public async Task<IActionResult> TestAdmin()
        {
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            return Json(new { 
                adminCount = adminUsers.Count(),
                admins = adminUsers.Select(a => new { id = a.Id, username = a.UserName, email = a.Email })
            });
        }

        // Lấy tin nhắn với admin
        [HttpGet]
        public async Task<IActionResult> GetAdminConversation()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                // Tìm admin đầu tiên
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                if (!adminUsers.Any())
                {
                    return BadRequest("Không có admin nào trong hệ thống.");
                }

                var admin = adminUsers[0];
                
                // Debug: Log thông tin
                Console.WriteLine($"Getting conversation between user {user.Id} and admin {admin.Id}");
                Console.WriteLine($"Admin username: {admin.UserName}, Email: {admin.Email}");
                
                // Kiểm tra role của admin
                var adminRoles = await _userManager.GetRolesAsync(admin);
                Console.WriteLine($"Admin roles: {string.Join(", ", adminRoles)}");
                
                var messages = await _messageRepository.GetConversationAsync(user.Id, admin.Id);
                
                // Debug: Log số lượng tin nhắn
                Console.WriteLine($"Found {messages.Count()} messages");
                
                await _messageRepository.MarkMessagesAsReadAsync(user.Id, admin.Id);

                var model = new ConversationViewModel
                {
                    Contact = admin,
                    Messages = messages,
                    UserId = user.Id,
                    ContactId = admin.Id,
                    IsAdminContact = true
                };

                // Debug: Log model data
                Console.WriteLine($"Returning conversation model - Contact: {admin.UserName}, Messages: {messages.Count()}, UserId: {user.Id}, ContactId: {admin.Id}");

                return PartialView("_ConversationPartial", model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAdminConversation: {ex.Message}");
                return BadRequest($"Lỗi khi lấy cuộc trò chuyện: {ex.Message}");
            }
        }
    }
}