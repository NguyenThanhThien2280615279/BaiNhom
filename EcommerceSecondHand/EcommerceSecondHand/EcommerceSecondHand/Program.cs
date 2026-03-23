using EcommerceSecondHand.Data;
using EcommerceSecondHand.Hubs;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories;
using EcommerceSecondHand.Repositories.Interfaces;
using EcommerceSecondHand.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using EcommerceSecondHand.Infrastructure;

var builder = WebApplication.CreateBuilder(args);


Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Cấu hình globalization và localization
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("vi-VN"),
    };

    options.DefaultRequestCulture = new RequestCulture("vi-VN");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// Thêm dịch vụ vào container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Cấu hình đường dẫn cookie cho MVC AccountController
builder.Services.ConfigureApplicationCookie(options =>
{
    // Đặt tên cookie riêng cho Máy A
    options.Cookie.Name = "ESH.Server.Auth";

    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";

    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});



// Chỉ dùng MVC Controllers + Views (đã bỏ Razor Pages Identity UI)
builder.Services.AddControllersWithViews(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
})
.AddViewLocalization()
.AddDataAnnotationsLocalization();

// Thêm SignalR
builder.Services.AddSignalR();



// Đăng ký các repository
builder.Services.AddScoped<BlockchainReputationService>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IUserStatisticsRepository, UserStatisticsRepository>();
builder.Services.AddScoped<ISystemStatisticsRepository, SystemStatisticsRepository>();
builder.Services.AddScoped<IRechargePackageRepository, RechargePackageRepository>();
builder.Services.AddScoped<IRechargeTransactionRepository, RechargeTransactionRepository>();
builder.Services.AddScoped<VnPayService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();

builder.Services.AddSingleton<ConnectionRegistry>();
builder.Services.AddSingleton<IServerMessageBus, ServerMessageBus>();
builder.Services.AddSingleton<SocketServerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SocketServerService>());

builder.Services.AddScoped<EcommerceSecondHand.Services.BlockchainReputationService>();



var app = builder.Build();

// Sử dụng localization
app.UseRequestLocalization();


if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    // KHÔNG bật HTTPS trong DEV để máy LAN vào bằng http://
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection(); // chỉ redirect HTTPS ở Production
}


app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Middleware để cập nhật thời gian hoạt động cuối cùng của user
app.UseMiddleware<EcommerceSecondHand.Middleware.UpdateLastActiveMiddleware>();

// Cấu hình endpoint hubs
app.MapHub<ChatHub>("/chatHub");
app.MapHub<NotificationHub>("/notificationHub");

app.MapControllers();

// Route cho MVC
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");


app.MapControllerRoute(
    name: "vendor_order_details",
    pattern: "Vendor/Orders/Details/{id}",
    defaults: new { controller = "Vendor", action = "OrderDetails" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Lifetime.ApplicationStarted.Register(async () =>
{
	using var scope = app.Services.CreateScope();
	var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
	var user = await userManager.FindByEmailAsync("admin@secondhand.com");
	if (user != null)
	{
		var token = await userManager.GeneratePasswordResetTokenAsync(user);
		await userManager.ResetPasswordAsync(user, token, "Admin@123456");
		// Optionally log result
        Console.WriteLine("Admin password reset successfully");
	}
});

await app.RunAsync();
