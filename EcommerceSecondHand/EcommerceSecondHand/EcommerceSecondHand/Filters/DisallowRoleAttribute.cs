using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EcommerceSecondHand.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class DisallowRoleAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _role;

        public DisallowRoleAttribute(string role)
        {
            _role = role;
        }

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated == true && user.IsInRole(_role))
            {
                // For Admin specifically, redirect to Admin dashboard for better UX
                if (_role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    context.Result = new RedirectResult("/Admin/Admin/Index");
                }
                else
                {
                    context.Result = new ForbidResult();
                }
            }
            return Task.CompletedTask;
        }
    }
}


