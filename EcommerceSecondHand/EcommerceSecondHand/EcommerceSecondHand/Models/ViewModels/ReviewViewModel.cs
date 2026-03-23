using System.ComponentModel.DataAnnotations;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class ReviewViewModel
    {
        public int ProductId { get; set; }
        
        [Required(ErrorMessage = "Vui lòng chọn số sao đánh giá.")]
        [Range(1, 5, ErrorMessage = "Số sao đánh giá phải từ 1 đến 5.")]
        [Display(Name = "Đánh giá")]
        public int Rating { get; set; }
        
        [Required(ErrorMessage = "Vui lòng nhập nội dung đánh giá.")]
        [StringLength(1000, ErrorMessage = "Nội dung đánh giá không được vượt quá 1000 ký tự.")]
        [Display(Name = "Nội dung đánh giá")]
        public string Comment { get; set; } = string.Empty;
    }
}