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
using System.Transactions;

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

        public IEnumerable<Product> GetAllProducts()
        {
            using var connection = _dbFactory.CreateConnection();
            connection.Open();

            var sql = @"
        SELECT 
            p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.IsActive, p.CategoryId, p.CreatedAt, p.UpdatedAt,
            pi.Id, pi.ImageUrl, pi.Extension, pi.SizeInBytes, pi.IsPrimary, pi.ProductId, pi.CreatedAt, pi.UpdatedAt
        FROM Products p
        LEFT JOIN ProductImage pi ON pi.ProductId = p.Id
        ORDER BY p.CreatedAt DESC
    ";

            var productDict = new Dictionary<int, Product>();

            var products = connection.Query<Product, ProductImage, Product>(
                sql,
                (product, image) =>
                {
                    if (!productDict.TryGetValue(product.Id, out var currentProduct))
                    {
                        currentProduct = product;
                        currentProduct.Images = new List<ProductImage>();
                        productDict.Add(currentProduct.Id, currentProduct);
                    }

                    if (image != null)
                    {
                        currentProduct.Images.Add(image);
                    }

                    return currentProduct;
                },
                splitOn: "Id"
            );

            return productDict.Values;
        }

        public IEnumerable<Admin> GetAllAdmins()
        {
            using var conn = _dbFactory.CreateConnection();
            conn.Open();

            var sql = @"
        SELECT 
            a.Id, a.FullName, a.Username, a.Email, a.Phone, a.RoleId,
            r.Id, r.Name, r.Description
        FROM Admins a
        INNER JOIN Roles r ON a.RoleId = r.Id";

   
            var admins = conn.Query<Admin, Role, Admin>(
                sql,
                (admin, role) =>
                {
                    admin.Role = role;
                    return admin;
                },
                splitOn: "Id" 
            );

            return admins;
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

               
                if (!string.IsNullOrEmpty(category.ImageUrl))
                {
                    var filePath = Path.Combine(_environment.WebRootPath, category.ImageUrl.TrimStart('/'));
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

               
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

        public async Task<(bool Success, string Message)> AddProductAsync(Product product, IEnumerable<IFormFile> images)
        {
            if (product == null)
                return (false, "Product cannot be null.");

            try
            {
                using var connection = (SqlConnection)_dbFactory.CreateConnection();
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

           
                var insertProductSql = @"
            INSERT INTO Products 
            (Name, Description, Price, StockQuantity, IsActive, CategoryId, CreatedAt, UpdatedAt)
            VALUES 
            (@Name, @Description, @Price, @StockQuantity, @IsActive, @CategoryId, @CreatedAt, @UpdatedAt);
            SELECT CAST(SCOPE_IDENTITY() as int);
        ";

                product.CreatedAt = DateTime.UtcNow;
                product.UpdatedAt = DateTime.UtcNow;

                var productId = await connection.QuerySingleAsync<int>(insertProductSql, product, transaction);

     
                if (images != null && images.Any())
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads/products");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    foreach (var file in images)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLower();
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                        if (!allowedExtensions.Contains(extension))
                        {
                            throw new InvalidOperationException("Only image files (.jpg, .jpeg, .png, .gif, .webp) are allowed.");
                        }

                        var fileName = $"{Guid.NewGuid()}{extension}";
                        var filePath = Path.Combine(uploadsFolder, fileName);

                        // Save file
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Insert into ProductImages
                        var insertImageSql = @"
                    INSERT INTO ProductImage
                    (ProductId, ImageUrl, Extension, SizeInBytes, IsPrimary, CreatedAt, UpdatedAt)
                    VALUES
                    (@ProductId, @ImageUrl, @Extension, @SizeInBytes, @IsPrimary, @CreatedAt, @UpdatedAt)
                ";

                        await connection.ExecuteAsync(insertImageSql, new
                        {
                            ProductId = productId,
                            ImageUrl = $"/uploads/products/{fileName}",
                            Extension = extension.Replace(".", ""),
                            SizeInBytes = file.Length,
                            IsPrimary = product.Images.Count == 0, // first image primary
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }, transaction);
                    }
                }

                transaction.Commit();
                return (true, "Product added successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        }

        public async Task<Product> GetProductByIdAsync(int id)
        {
            using var conn = _dbFactory.CreateConnection();

            string sql = @"
                SELECT * FROM Product WHERE Id = @Id;
                SELECT * FROM ProductImage WHERE ProductId = @Id;";

            using var multi = await conn.QueryMultipleAsync(sql, new { Id = id });


            var product = await multi.ReadSingleOrDefaultAsync<Product>();
            if (product != null)
            {
                var images = (await multi.ReadAsync<ProductImage>()).ToList();
                product.Images = images;
            }

            return product;
        }

        public async Task<bool> UpdateProductAsync(Product product, List<int> deleteImageIds = null, IEnumerable<IFormFile> newImages = null)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            using var conn = (SqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var tran = conn.BeginTransaction();

            try
            {
         
                string updateProductSql = @"
            UPDATE Products
            SET Name = @Name,
                Description = @Description,
                Price = @Price,
                StockQuantity = @StockQuantity,
                CategoryId = @CategoryId,
                IsActive = @IsActive,
                UpdatedAt = GETDATE()
            WHERE Id = @Id";

                await conn.ExecuteAsync(updateProductSql, product, tran);

             
                if (deleteImageIds != null && deleteImageIds.Any())
                {
        
                    var selectSql = "SELECT ImageUrl FROM ProductImage WHERE Id IN @Ids";
                    var filesToDelete = await conn.QueryAsync<string>(selectSql, new { Ids = deleteImageIds }, tran);

                    foreach (var relativePath in filesToDelete)
                    {
                        var fullPath = Path.Combine(_environment.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(fullPath))
                            File.Delete(fullPath);
                    }

           
                    string deleteImagesSql = "DELETE FROM ProductImage WHERE Id IN @Ids";
                    await conn.ExecuteAsync(deleteImagesSql, new { Ids = deleteImageIds }, tran);
                }

        
                if (newImages != null && newImages.Any())
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads/products");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    foreach (var file in newImages)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLower();
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                        if (!allowedExtensions.Contains(extension))
                        {
                            throw new InvalidOperationException("Only image files (.jpg, .jpeg, .png, .gif, .webp) are allowed.");
                        }

                        var fileName = $"{Guid.NewGuid()}{extension}";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        var IsPrimary = 0;
               
                        using var stream = new FileStream(filePath, FileMode.Create);
                        await file.CopyToAsync(stream);

                     
                        var insertImageSql = @"
                    INSERT INTO ProductImage
                    (ProductId, ImageUrl, Extension, SizeInBytes, IsPrimary, CreatedAt, UpdatedAt)
                    VALUES
                    (@ProductId, @ImageUrl, @Extension, @SizeInBytes, @IsPrimary, GETDATE(), GETDATE())";

                        await conn.ExecuteAsync(insertImageSql, new
                        {
                            ProductId = product.Id,
                            ImageUrl = $"/uploads/products/{fileName}",
                            Extension = extension.Replace(".", ""),
                            SizeInBytes = file.Length,
                            IsPrimary = IsPrimary
                        }, tran);
                    }
                }

                tran.Commit();
                return true;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        public async Task<bool> DeleteProductAsync(int productId)
        {
            if (productId <= 0)
                throw new ArgumentException("Invalid product ID", nameof(productId));

            using var conn = (SqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var tran = conn.BeginTransaction();

            try
            {
              
                var selectImagesSql = "SELECT ImageUrl FROM ProductImage WHERE ProductId = @ProductId";
                var imageUrls = await conn.QueryAsync<string>(selectImagesSql, new { ProductId = productId }, tran);

      
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads/products");
                foreach (var url in imageUrls)
                {
                    var fullPath = Path.Combine(_environment.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                }

            
                var deleteImagesSql = "DELETE FROM ProductImage WHERE ProductId = @ProductId";
                await conn.ExecuteAsync(deleteImagesSql, new { ProductId = productId }, tran);

    
                var deleteProductSql = "DELETE FROM Products WHERE Id = @Id";
                var affectedRows = await conn.ExecuteAsync(deleteProductSql, new { Id = productId }, tran);

                tran.Commit();
                return affectedRows > 0;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }


    }

}
