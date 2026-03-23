using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Tên là bắt buộc")]
        [Display(Name = "Tên")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ là bắt buộc")]
        [Display(Name = "Họ")]
        public string LastName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Display(Name = "Thành phố")]
        public string? City { get; set; }

        [Display(Name = "Quốc gia")]
        public string? Country { get; set; }

        [Display(Name = "Mã bưu điện")]
        public string? PostalCode { get; set; }

        [Display(Name = "Giới thiệu")]
        public string? Bio { get; set; }

        [Display(Name = "Ảnh đại diện hiện tại")]
        public string? ProfilePicture { get; set; }

        [Display(Name = "Tải lên ảnh đại diện mới")]
        public IFormFile? ProfilePictureFile { get; set; }
    }
}