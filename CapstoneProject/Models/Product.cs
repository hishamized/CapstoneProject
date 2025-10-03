using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapstoneProject.Models
{
    [Index(nameof(Name), IsUnique = true)]
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } 

        [MaxLength(500)]
        public string Description { get; set; } 

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } 

        [Required]
        public int StockQuantity { get; set; } = 0; 


        public bool IsActive { get; set; } = true; 

        // Foreign Key to Category
        [Required]
        public int CategoryId { get; set; }
        public Category? Category { get; set; } 

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    }
}
