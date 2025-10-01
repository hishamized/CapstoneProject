using CapstoneProject.Interfaces;
using CapstoneProject.JWT;
using CapstoneProject.Models;
using CapstoneProject.Services.Data;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace CapstoneProject.Services
{
    public class AdminService : IAdminService
    {
        private readonly IDbConnectionFactory _dbFactory;
        private readonly JwtSettings _jwtSettings;
        private readonly IWebHostEnvironment _environment;



        public AdminService(IDbConnectionFactory dbFactory, IOptions<JwtSettings> jwtOptions, IWebHostEnvironment environment)
        {
            _dbFactory = dbFactory;
            _jwtSettings = jwtOptions.Value;
            _environment = environment;
        }

        public LoginResult Login(string login, string password)
        {
            using var conn = _dbFactory.CreateConnection();
            conn.Open();

            var admin = conn.QueryFirstOrDefault<Admin>(
                @"SELECT * FROM Admins 
          WHERE Username = @Login OR Email = @Login OR Phone = @Login",
                new { Login = login }
            );

            if (admin == null || !BCrypt.Net.BCrypt.Verify(password, admin.HashedPassword))
            {
                return new LoginResult
                {
                    Success = false,
                    Message = "Invalid credentials"
                };
            }

            string jwt = GenerateJwtToken(admin.Username);

            return new LoginResult
            {
                Success = true,
                Admin = admin,
                JwtToken = jwt
            };
        }


        public string GenerateJwtToken(string username)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, "Admin")
                }),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiresMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public void Logout(HttpContext httpContext)
        {
            httpContext.Session.Clear();
            httpContext.Response.Cookies.Delete("AdminUsername");
            httpContext.Response.Cookies.Delete("AdminJwtToken");
        }

        public IEnumerable<SelectListItem> GetRoles()
        {
            using var conn = _dbFactory.CreateConnection();
            conn.Open();

            var roles = conn.Query<Role>("SELECT Id, Name FROM Roles");

            return roles.Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = r.Name
            });
        }

        public ServiceResult AddAdmin(Admin admin)
        {
            using var conn = _dbFactory.CreateConnection();
            conn.Open();

            var existing = conn.QueryFirstOrDefault<Admin>(
                @"SELECT * FROM Admins 
              WHERE Username = @Username OR Email = @Email OR Phone = @Phone",
                new { admin.Username, admin.Email, admin.Phone });

            if (existing != null)
            {
                return new ServiceResult
                {
                    Success = false,
                    Message = "Username, Email, or Phone already exists."
                };
            }

      
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(admin.HashedPassword);

            var rows = conn.Execute(
                @"INSERT INTO Admins (FullName, Username, Email, Phone, HashedPassword, RoleId, CreatedAt, UpdatedAt)
              VALUES (@FullName, @Username, @Email, @Phone, @HashedPassword, @RoleId, @CreatedAt, @UpdatedAt)",
                new
                {
                    admin.FullName,
                    admin.Username,
                    admin.Email,
                    admin.Phone,
                    HashedPassword = hashedPassword,
                    admin.RoleId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            if (rows > 0)
            {
                return new ServiceResult
                {
                    Success = true,
                    Message = "Admin added successfully."
                };
            }

            return new ServiceResult
            {
                Success = false,
                Message = "Failed to add admin. Please try again."
            };
        }

        public (bool Success, string Message) AddCategory(Category category, IFormFile? imageFile)
        {
            try
            {
  
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "images/categories");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var extension = Path.GetExtension(imageFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(extension))
                    {
                        return (false, "Only image files (.jpg, .jpeg, .png, .gif, .webp) are allowed.");
                    }

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        imageFile.CopyTo(fileStream);
                    }

               
                    category.ImageUrl = "/images/categories/" + uniqueFileName;
                }

                using var connection = _dbFactory.CreateConnection();
                connection.Open();

                var sql = @"
                INSERT INTO Categories (Name, Description, ImageUrl, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Name, @Description, @ImageUrl, @IsActive, @CreatedAt, @UpdatedAt);
            ";
                category.CreatedAt = DateTime.UtcNow;
                category.UpdatedAt = DateTime.UtcNow;


                int rows = connection.Execute(sql, new
                {
                    category.Name,
                    category.Description,
                    category.ImageUrl,
                    category.IsActive,
                    category.CreatedAt,
                    category.UpdatedAt
                });

                if (rows > 0)
                    return (true, "Category added successfully.");
                else
                    return (false, "Failed to add category.");
            }
            catch (Exception ex)
            {
                // Log exception as needed
                return (false, $"Error: {ex.Message}");
            }

        }
        public IEnumerable<Category> GetAllCategories()
        {
            using var connection = _dbFactory.CreateConnection();
            connection.Open();

            var sql = "SELECT * FROM Categories ORDER BY CreatedAt DESC";
            var categories = connection.Query<Category>(sql);

            return categories;
        }

        public (bool Success, string Message) UpdateCategory(Category updatedCategory, IFormFile? newImageFile, bool deleteImage)
        {
            try
            {
                using var connection = _dbFactory.CreateConnection();
                connection.Open();

                var oldCategory = connection.QueryFirstOrDefault<Category>(
                    "SELECT * FROM Categories WHERE Id = @Id", new { updatedCategory.Id });

                if (oldCategory == null)
                    return (false, "Category not found.");

            
                if (deleteImage && !string.IsNullOrEmpty(oldCategory.ImageUrl))
                {
                    var oldFilePath = Path.Combine(_environment.WebRootPath, oldCategory.ImageUrl.TrimStart('/'));
                    if (File.Exists(oldFilePath))
                        File.Delete(oldFilePath);

                    updatedCategory.ImageUrl = null;
                }
                else
                {
                    updatedCategory.ImageUrl = oldCategory.ImageUrl;
                }

                // replace image
                if (newImageFile != null && newImageFile.Length > 0)
                {
                    if (!string.IsNullOrEmpty(oldCategory.ImageUrl))
                    {
                        var oldFilePath = Path.Combine(_environment.WebRootPath, oldCategory.ImageUrl.TrimStart('/'));
                        if (File.Exists(oldFilePath))
                            File.Delete(oldFilePath);
                    }

                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "images/categories");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid() + Path.GetExtension(newImageFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);


                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var extension = Path.GetExtension(newImageFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(extension))
                    {
                        return (false, "Only image files (.jpg, .jpeg, .png, .gif, .webp) are allowed.");
                    }

                    using var fileStream = new FileStream(filePath, FileMode.Create);
                    newImageFile.CopyTo(fileStream);

                    updatedCategory.ImageUrl = "/images/categories/" + uniqueFileName;
                }

                updatedCategory.UpdatedAt = DateTime.UtcNow;

                var sql = @"
            UPDATE Categories
            SET Name = @Name,
                Description = @Description,
                ImageUrl = @ImageUrl,
                IsActive = @IsActive,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
        ";

                int rows = connection.Execute(sql, updatedCategory);

                return rows > 0
                    ? (true, "Category updated successfully.")
                    : (false, "Update failed.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }

        public (bool Success, string Message) DeleteCategory(int categoryId)
        {
            try
            {
                using var connection = _dbFactory.CreateConnection();
                connection.Open();

                var category = connection.QueryFirstOrDefault<Category>(
                    "SELECT * FROM Categories WHERE Id = @Id", new { Id = categoryId });

                if (category == null)
                    return (false, "Category not found.");

                // Delete image file if exists
                if (!string.IsNullOrEmpty(category.ImageUrl))
                {
                    var filePath = Path.Combine(_environment.WebRootPath, category.ImageUrl.TrimStart('/'));
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

                // Delete from DB
                int rows = connection.Execute("DELETE FROM Categories WHERE Id = @Id", new { Id = categoryId });

                return rows > 0
                    ? (true, "Category deleted successfully.")
                    : (false, "Failed to delete category.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }


    }

}
