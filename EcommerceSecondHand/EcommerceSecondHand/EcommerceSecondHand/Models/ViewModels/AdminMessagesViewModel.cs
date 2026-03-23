using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class AdminMessagesViewModel
    {
        public IEnumerable<ApplicationUser> Contacts { get; set; } = new List<ApplicationUser>();
        public ApplicationUser? SelectedContact { get; set; }
        public IEnumerable<Message> Messages { get; set; } = new List<Message>();
        public string AdminId { get; set; } = string.Empty;
        public string? ContactId { get; set; }
    }
}
