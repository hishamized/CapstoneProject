using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapstoneProject.Models
{
    [Index(nameof(Name), IsUnique = true)]
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } // Category name, e.g., "Electronics"

        [MaxLength(250)]
        public string Description { get; set; } // Optional description

        public string ImageUrl { get; set; } // Optional image for category display

        public bool IsActive { get; set; } = true; // For soft-deleting or deactivating categories

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property if you want to relate products later
        public ICollection<Product>? Products { get; set; }
    }
}
