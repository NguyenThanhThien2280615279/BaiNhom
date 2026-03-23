using EcommerceSecondHand.Models;
using Microsoft.AspNetCore.Identity;

namespace EcommerceSecondHand.Middleware
{
    public class UpdateLastActiveMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UpdateLastActiveMiddleware> _logger;

        public UpdateLastActiveMiddleware(RequestDelegate next, ILogger<UpdateLastActiveMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
        {
            // Chỉ cập nhật cho user đã đăng nhập và không phải request API
            if (context.User.Identity?.IsAuthenticated == true && 
                !context.Request.Path.StartsWithSegments("/api"))
            {
                try
                {
                    var user = await userManager.GetUserAsync(context.User);
                    if (user != null)
                    {
                        // Chỉ cập nhật nếu đã qua ít nhất 1 phút từ lần cập nhật cuối
                        if (user.LastActiveAt == null || 
                            DateTime.UtcNow - user.LastActiveAt.Value > TimeSpan.FromMinutes(1))
                        {
                            user.LastActiveAt = DateTime.UtcNow;
                            await userManager.UpdateAsync(user);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating user last active time");
                }
            }

            await _next(context);
        }
    }
}
