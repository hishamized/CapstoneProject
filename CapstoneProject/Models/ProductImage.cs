using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapstoneProject.Models
{
    public class ProductImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; }  // stored path

        [MaxLength(10)]
        public string? Extension { get; set; } // jpg, png, webp

        public long? SizeInBytes { get; set; } // file size

        public bool IsPrimary { get; set; } = false; // main image flag

        [Required]
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
