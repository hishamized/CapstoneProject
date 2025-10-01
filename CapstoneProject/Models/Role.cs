using System.ComponentModel.DataAnnotations;

namespace CapstoneProject.Models
{
    public class Role
    {
        [Required]
        public int? Id { get; set; }

        public string Name { get; set; }

        // Optional description of the role
        public string? Description { get; set; }

        // Navigation property to Admins
        public ICollection<Admin> Admins { get; set; }
    }
}
