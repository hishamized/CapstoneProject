using CapstoneProject.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CapstoneProject.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        { }

        public DbSet<Admin> Admins { get; set; }
        public DbSet<Role> Roles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed default roles
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Master" },
                new Role { Id = 2, Name = "Manager" },
                new Role { Id = 3, Name = "Employee" },
                new Role { Id = 4, Name = "Supervisor" },
                new Role { Id = 5, Name = "BDE" }
            );

            modelBuilder.Entity<Admin>().HasData(
               new Admin
               {
                   Id = 1,
                   FullName = "System Master Admin",
                   Username = "masteradmin",
                   Email = "master@capstone.com",
                   Phone = "1234567890",
                   RoleId = 1, // Master Role
                   HashedPassword = "$2a$11$zGPseXffSGSUia3dzDi5Xu0.WpkGxrR8IeJASQMzIx6PqXlgIMOu.",
                   CreatedAt = new DateTime(2025, 09, 30, 12, 0, 0), 
                   UpdatedAt = new DateTime(2025, 09, 30, 12, 0, 0) 
               }
                   );
        }
    }
}
