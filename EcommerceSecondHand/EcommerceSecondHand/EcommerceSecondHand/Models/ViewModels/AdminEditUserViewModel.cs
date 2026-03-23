using System.ComponentModel.DataAnnotations;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class AdminEditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ là bắt buộc")]
        [StringLength(50, ErrorMessage = "Họ không được vượt quá 50 ký tự")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên là bắt buộc")]
        [StringLength(50, ErrorMessage = "Tên không được vượt quá 50 ký tự")]
        public string LastName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string? PhoneNumber { get; set; }

        [StringLength(200, ErrorMessage = "Địa chỉ không được vượt quá 200 ký tự")]
        public string? Address { get; set; }

        [StringLength(50, ErrorMessage = "Thành phố không được vượt quá 50 ký tự")]
        public string? City { get; set; }

        [StringLength(50, ErrorMessage = "Quốc gia không được vượt quá 50 ký tự")]
        public string? Country { get; set; }

        [StringLength(10, ErrorMessage = "Mã bưu điện không được vượt quá 10 ký tự")]
        public string? PostalCode { get; set; }

        [StringLength(500, ErrorMessage = "Giới thiệu không được vượt quá 500 ký tự")]
        public string? Bio { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Số dư ví phải lớn hơn hoặc bằng 0")]
        public decimal WalletBalance { get; set; }

        public bool EmailConfirmed { get; set; }
        public bool LockoutEnabled { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }

        public string? CurrentRole { get; set; }
        public IList<string> AvailableRoles { get; set; } = new List<string>();
    }
}
