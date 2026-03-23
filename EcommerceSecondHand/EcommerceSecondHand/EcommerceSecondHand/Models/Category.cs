using System.ComponentModel.DataAnnotations;

namespace EcommerceSecondHand.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(200)]
        public string? Description { get; set; }
        
        public string? ImageUrl { get; set; }
        
        // Navigation property
        public virtual ICollection<Product>? Products { get; set; }
    }
}