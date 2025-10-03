using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
namespace CapstoneProject.Middleware
{
    public class AdminAuthMiddleware
    {
        private readonly RequestDelegate _next;

        public AdminAuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.ToString().ToLower();
            var publicPaths = new[] { "/admin/login", "/admin/logout" };

            // Protect /AdminDashboard routes only
            if (!publicPaths.Contains(path) && path.StartsWith("/admin"))
            {
                // Check session
                if (context.Session.GetString("AdminUsername") == null)
                {
                    // Check JWT
                    string token = context.Request.Cookies["AdminJwtToken"];
                    if (string.IsNullOrEmpty(token))
                    {
                        context.Response.Redirect("/Admin/Login?message=You must log in first");
                        return;
                    }

                    // TODO: Optionally validate JWT here if needed
                }
            }

            await _next(context);
        }
    }
}


