using CapstoneProject.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CapstoneProject.Interfaces
{
    public interface IAdminService
    {
        LoginResult Login(string login, string password);
        string GenerateJwtToken(string username);
        void Logout(HttpContext httpContext);
        IEnumerable<SelectListItem> GetRoles();
        ServiceResult AddAdmin(Admin admin);
        (bool Success, string Message) AddCategory(Category category, IFormFile? imageFile);

        (bool Success, string Message) UpdateCategory(Category category, IFormFile? newImage, bool deleteOldImage);

        (bool Success, string Message) DeleteCategory(int categoryId);

        Task<(bool Success, string Message)> AddProductAsync(Product product, IEnumerable<IFormFile> images);

        IEnumerable<Category> GetAllCategories();

        IEnumerable<Product> GetAllProducts();

        IEnumerable<Admin> GetAllAdmins();

        Task<Product> GetProductByIdAsync(int id);
        Task<bool> UpdateProductAsync(Product product, List<int> deleteImageIds = null, IEnumerable<IFormFile> newImages = null);

        Task<bool> DeleteProductAsync(int productId);
    }

    public class LoginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Admin Admin { get; set; }
        public string JwtToken { get; set; }
    }
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
