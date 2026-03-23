using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace EcommerceSecondHand.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IMessageRepository _messageRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationRepository _notificationRepository;
        private static readonly Dictionary<string, string> _userConnections = new();
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public ChatHub(
            IMessageRepository messageRepository,
            UserManager<ApplicationUser> userManager,
            INotificationRepository notificationRepository)
        {
            _messageRepository = messageRepository;
            _userManager = userManager;
            _notificationRepository = notificationRepository;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                _userConnections[userId] = Context.ConnectionId;
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.Identity?.Name;
            if (!string.IsNullOrEmpty(userId) && _userConnections.ContainsKey(userId))
            {
                _userConnections.Remove(userId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(string receiverId, string message)
        {
            var senderId = Context.User?.Identity?.Name;
            if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(receiverId))
            {
                return;
            }

            var sender = await _userManager.FindByNameAsync(senderId);
            var receiver = await _userManager.FindByIdAsync(receiverId);

            if (sender == null || receiver == null)
            {
                return;
            }

            // Lưu tin nhắn vào cơ sở dữ liệu
            var newMessage = new Message
            {
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                Content = message,
                Timestamp = DateTime.UtcNow,
                DateSent = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            await _messageRepository.AddAsync(newMessage);
            await _messageRepository.SaveAsync();

            // Tạo thông báo cho người nhận
            var notification = new Notification
            {
                UserId = receiver.Id,
                Title = "Tin nhắn mới",
                Message = $"Bạn có tin nhắn mới từ {sender.FirstName} {sender.LastName}",
                Type = NotificationType.Message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = newMessage.Id.ToString(),
                ActionUrl = $"/Messages?contactId={sender.Id}"
            };

            await _notificationRepository.AddAsync(notification);
            await _notificationRepository.SaveAsync();

            // Gửi đến người dùng cụ thể nếu họ đang online
            if (_userConnections.TryGetValue(receiver.UserName!, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync("ReceiveMessage", 
                    new { 
                        messageId = newMessage.Id,
                        senderId = sender.Id,
                        senderName = $"{sender.FirstName} {sender.LastName}",
                        senderProfilePicture = sender.ProfilePicture,
                        message = message,
                        sentAt = newMessage.Timestamp.ToString(DateTimeFormat)
                    });
                
                await Clients.Client(connectionId).SendAsync("ReceiveNotification", 
                    new { 
                        notificationId = notification.Id,
                        title = notification.Title,
                        message = notification.Message,
                        type = notification.Type.ToString(),
                        createdAt = notification.CreatedAt.ToString(DateTimeFormat),
                        actionUrl = notification.ActionUrl
                    });
            }

            // Đồng thời gửi lại cho người gửi để cập nhật giao diện người dùng của họ
            await Clients.Caller.SendAsync("MessageSent", 
                new { 
                    messageId = newMessage.Id,
                    receiverId = receiver.Id,
                    receiverName = $"{receiver.FirstName} {receiver.LastName}",
                    message = message,
                    sentAt = newMessage.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                });
        }

        public async Task MarkMessageAsRead(int messageId)
        {
            await _messageRepository.MarkAsReadAsync(messageId);
            
            // Lấy tin nhắn để thông báo cho người gửi
            var message = await _messageRepository.GetByIdAsync(messageId);
            if (message != null && _userConnections.TryGetValue(message.Sender?.UserName!, out var senderConnectionId))
            {
                await Clients.Client(senderConnectionId).SendAsync("MessageRead", messageId);
            }
        }

        // Giữ API công khai tối thiểu cho chat user-vendor
        public async Task SendMessageFromAdmin(string receiverId, string message)
        {
            var adminId = Context.User?.Identity?.Name;
            if (string.IsNullOrEmpty(adminId))
            {
                return;
            }

            var admin = await _userManager.FindByNameAsync(adminId);
            var receiver = await _userManager.FindByIdAsync(receiverId);

            if (admin == null || receiver == null)
            {
                return;
            }

            // Kiểm tra quyền admin
            if (!await _userManager.IsInRoleAsync(admin, "Admin"))
            {
                return;
            }

            // Lưu tin nhắn vào cơ sở dữ liệu
            var newMessage = new Message
            {
                SenderId = admin.Id,
                ReceiverId = receiver.Id,
                Content = message,
                Timestamp = DateTime.UtcNow,
                DateSent = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            await _messageRepository.AddAsync(newMessage);
            await _messageRepository.SaveAsync();

            // Tạo thông báo cho người nhận
            var notification = new Notification
            {
                UserId = receiver.Id,
                Title = "Tin nhắn từ Admin",
                Message = $"Bạn có tin nhắn mới từ Admin",
                Type = NotificationType.Message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = newMessage.Id.ToString(),
                ActionUrl = $"/Messages?contactId={admin.Id}"
            };

            await _notificationRepository.AddAsync(notification);
            await _notificationRepository.SaveAsync();

            // Gửi đến người nhận nếu đang online
            if (_userConnections.TryGetValue(receiver.UserName!, out var receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", 
                    new { 
                        messageId = newMessage.Id,
                        senderId = admin.Id,
                        senderName = "Admin",
                        senderProfilePicture = admin.ProfilePicture,
                        content = message,
                        timestamp = newMessage.Timestamp.ToString("HH:mm")
                    });
                
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveNotification", 
                    new { 
                        notificationId = notification.Id,
                        title = notification.Title,
                        message = notification.Message,
                        type = notification.Type.ToString(),
                        createdAt = notification.CreatedAt.ToString(DateTimeFormat),
                        actionUrl = notification.ActionUrl
                    });
            }

            // Xác nhận gửi tin nhắn cho admin
            await Clients.Caller.SendAsync("MessageSentFromAdmin", 
                new { 
                    messageId = newMessage.Id,
                    receiverId = receiver.Id,
                    receiverName = $"{receiver.FirstName} {receiver.LastName}",
                    message = message,
                    sentAt = newMessage.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                });
        }

        // Phương thức lấy role của user
        private async Task<string> GetUserRole(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return roles.Count > 0 ? roles[0] : "User";
        }
    }
}