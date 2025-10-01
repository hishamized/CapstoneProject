using CapstoneProject.Interfaces;
using CapstoneProject.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CapstoneProject.Controllers
{
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        [HttpGet]
        public IActionResult Login(string message = null)
        {
            ViewBag.Message = message;
            return View();
        }


        [HttpPost]
        public IActionResult Login(string login, string password)
        {
            var result = _adminService.Login(login, password);

            if (!result.Success)
            {
                return RedirectToAction("Login", new { message = result.Message });
            }

            // Only HTTP concerns left here:
            HttpContext.Session.SetString("AdminUsername", result.Admin.Username);

            HttpContext.Response.Cookies.Append("AdminUsername", result.Admin.Username, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.Now.AddMinutes(30)
            });

            HttpContext.Response.Cookies.Append("AdminJwtToken", result.JwtToken, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.Now.AddMinutes(30)
            });

            TempData["AdminName"] = result.Admin.FullName;
            TempData["AdminUsername"] = result.Admin.Username;
            TempData["AdminEmail"] = result.Admin.Email;

            return RedirectToAction("Dashboard");
        }


        [HttpGet]
        public IActionResult Dashboard()
        {
            ViewBag.Roles = _adminService.GetRoles();

            ViewBag.AdminUsername = HttpContext.Session.GetString("AdminUsername");
            return View();
        }

        [HttpGet]
        public IActionResult Logout()
        {
            _adminService.Logout(HttpContext);
            return RedirectToAction("Login", new { message = "Logged out successfully" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAdmin(Admin admin)
        {

            if (!ModelState.IsValid)
            {
                // Collect all errors into a single string
                var allErrors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
                    .ToList();

                // Join all errors into one string (or separate by <br> for HTML)
                TempData["ErrorMessage"] = string.Join(" | ", allErrors);

                return RedirectToAction("Dashboard");
            }


            var result = _adminService.AddAdmin(admin);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction("Dashboard");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddCategory(Category category, IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
            {
                // Collect errors
                var allErrors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
                    .ToList();

                TempData["ErrorMessage"] = string.Join(" | ", allErrors);
                return RedirectToAction("Dashboard"); // or wherever your form resides
            }

            var result = _adminService.AddCategory(category, imageFile);

            if (result.Success)
                TempData["SuccessMessage"] = result.Message;
            else
                TempData["ErrorMessage"] = result.Message;

            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public IActionResult ManageCategories()
        {
            var categories = _adminService.GetAllCategories();
            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateCategory(Category category, IFormFile? newImageFile, bool deleteImage = false)
        {
            // manual validation for required field(s)
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                TempData["Error"] = "Name is required.";
                return RedirectToAction("ManageCategories");
            }

            var result = _adminService.UpdateCategory(category, newImageFile, deleteImage);

            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;

            return RedirectToAction("ManageCategories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(int id)
        {
            var result = _adminService.DeleteCategory(id);

            if (result.Success)
                TempData["Success"] = result.Message;
            else
                TempData["Error"] = result.Message;

            return RedirectToAction("ManageCategories");
        }

    }
}
