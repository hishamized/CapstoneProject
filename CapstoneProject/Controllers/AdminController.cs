using BCrypt.Net;
using CapstoneProject.JWT;
using CapstoneProject.Models;
using CapstoneProject.Services.Data;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace CapstoneProject.Controllers
{
    public class AdminController : Controller
    {
        private readonly IDbConnectionFactory _dbFactory;
        private readonly IConfiguration _configuration;
        private readonly JwtSettings _jwtSettings;

        public AdminController(IDbConnectionFactory dbFactory, IConfiguration configuration, IOptions<JwtSettings> jwtOptions)
        {
            _dbFactory = dbFactory;
            _configuration = configuration;
            _jwtSettings = jwtOptions.Value;

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
            using var conn = _dbFactory.CreateConnection();
            conn.Open();

            // Query admin using Dapper: check username OR email OR phone
            var admin = conn.QueryFirstOrDefault<Admin>(
                @"SELECT * FROM Admins 
          WHERE Username = @Login OR Email = @Login OR Phone = @Login",
                new { Login = login }
            );

            if (admin == null || !BCrypt.Net.BCrypt.Verify(password, admin.HashedPassword))
            {
                return RedirectToAction("Login", new { message = "Invalid credentials" });
            }

            // ✅ Set session
            HttpContext.Session.SetString("AdminUsername", admin.Username);

            // ✅ Set cookie
            HttpContext.Response.Cookies.Append("AdminUsername", admin.Username, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.Now.AddMinutes(30)
            });

            string jwt = GenerateJwtToken(admin.Username);


            HttpContext.Response.Cookies.Append("AdminJwtToken", jwt, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTimeOffset.Now.AddMinutes(30)
            });

            return RedirectToAction("Dashboard");
        }

        [Authorize]
        [HttpGet]
        public IActionResult Dashboard()
        {
            ViewBag.AdminUsername = HttpContext.Session.GetString("AdminUsername");
            return View();
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("AdminUsername");
            Response.Cookies.Delete("AdminJwtToken");
            return RedirectToAction("Login", new { message = "Logged out successfully" });
        }

        private string GenerateJwtToken(string username)
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
    }
}
