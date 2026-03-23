using System.ComponentModel.DataAnnotations;
using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Models.ViewModels
{
    public class AddProductViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm.")]
        [StringLength(100, ErrorMessage = "Tên sản phẩm không được vượt quá 100 ký tự.")]
        [Display(Name = "Tên sản phẩm")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Vui lòng nhập mô tả sản phẩm.")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Vui lòng nhập giá sản phẩm.")]
        [Range(1000, 100000000, ErrorMessage = "Giá sản phẩm phải từ 1,000 đến 100,000,000.")]
        [Display(Name = "Giá")]
        public decimal Price { get; set; }
        
        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }
        
        [Required(ErrorMessage = "Vui lòng chọn tình trạng sản phẩm.")]
        [Display(Name = "Tình trạng")]
        public ProductCondition Condition { get; set; }
        
        [Display(Name = "Hình ảnh sản phẩm")]
        public IFormFile? ImageFile { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng.")]
        [Range(0, 100000, ErrorMessage = "Số lượng phải từ 0 đến 100,000.")]
        [Display(Name = "Số lượng")]
        public int Quantity { get; set; } = 1;
    }

    public class EditProductViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm.")]
        [StringLength(100, ErrorMessage = "Tên sản phẩm không được vượt quá 100 ký tự.")]
        [Display(Name = "Tên sản phẩm")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả sản phẩm.")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập giá sản phẩm.")]
        [Range(1000, 100000000, ErrorMessage = "Giá sản phẩm phải từ 1,000 đến 100,000,000.")]
        [Display(Name = "Giá")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tình trạng sản phẩm.")]
        [Display(Name = "Tình trạng")]
        public ProductCondition Condition { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng.")]
        [Range(0, 100000, ErrorMessage = "Số lượng phải từ 0 đến 100,000.")]
        [Display(Name = "Số lượng")]
        public int Quantity { get; set; }

        [Display(Name = "Hình ảnh hiện tại")]
        public string? CurrentImageUrl { get; set; }

        [Display(Name = "Thay đổi hình ảnh")]
        public IFormFile? ImageFile { get; set; }
    }
}