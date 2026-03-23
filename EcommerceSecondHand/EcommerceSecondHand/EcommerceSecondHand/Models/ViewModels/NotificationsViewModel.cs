using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class NotificationsViewModel
    {
        public IEnumerable<Notification> Notifications { get; set; } = new List<Notification>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int UnreadCount { get; set; }
        public NotificationType? SelectedType { get; set; }
        public bool UnreadOnly { get; set; }
    }
}