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
        public string Name { get; set; } // Product name

        [MaxLength(500)]
        public string Description { get; set; } // Optional product description

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } // Product price

        [Required]
        public int StockQuantity { get; set; } = 0; // Inventory count

        public string ImageUrl { get; set; } // Optional product image

        public bool IsActive { get; set; } = true; // For deactivating product instead of deleting

        // Foreign Key to Category
        [Required]
        public int CategoryId { get; set; }
        public Category? Category { get; set; } // Navigation property

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
