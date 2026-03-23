using EcommerceSecondHand.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSecondHand.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            
            try
            {
                var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
                var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // Drop and recreate database with latest migrations
                logger.LogInformation("Đang xóa cơ sở dữ liệu hiện tại (nếu có)...");
                await context.Database.EnsureDeletedAsync();
                logger.LogInformation("Đang áp dụng các migration cơ sở dữ liệu...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Cơ sở dữ liệu đã được làm mới.");

                // Roles
                logger.LogInformation("Seeding vai trò...");
                await SeedRoles(roleManager);
                
                // Admin
                logger.LogInformation("Seeding tài khoản Admin...");
                await SeedAdminUser(userManager);

                // Categories
                logger.LogInformation("Seeding danh mục...");
                await SeedCategories(context);

                // Vendors and Customers (multiple)
                logger.LogInformation("Seeding nhiều Vendor/Customer...");
                var (vendors, customers) = await SeedMultipleVendorsAndCustomers(userManager, 5, 20);

                // Products per vendor (second-hand, qty=1)
                logger.LogInformation("Seeding sản phẩm đã qua sử dụng...");
                await SeedProductsForVendors(context, vendors, 12);

                // Orders and related data
                logger.LogInformation("Seeding đơn hàng và giao dịch...");
                await SeedOrdersAndRelatedAsync(context, vendors, customers, 30);

                // Stats
                logger.LogInformation("Seeding thống kê hệ thống...");
                await SeedStatisticsAsync(context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Đã xảy ra lỗi khi tạo dữ liệu mẫu cho cơ sở dữ liệu.");
                throw;
            }
        }
        
        private static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            string[] roles = { "Admin", "Customer", "Vendor" };
            
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
        
        private static async Task SeedAdminUser(UserManager<ApplicationUser> userManager)
        {
            var adminEmail = "admin@ecommerce.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            
            if (adminUser == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User",
                    CreatedAt = DateTime.UtcNow,
                    RegistrationDate = DateTime.UtcNow,
                    LastLoginDate = DateTime.UtcNow
                };
                
                var result = await userManager.CreateAsync(admin, "Admin@123456");
                
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
                else
                {
                    throw new Exception($"Không thể tạo người dùng admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
        
        private static async Task SeedCategories(ApplicationDbContext context)
        {
            if (!await context.Categories.AnyAsync())
            {
                var categories = new List<Category>
                {
                    new Category { Name = "Electronics", Description = "Thiết bị điện tử và tiện ích" },
                    new Category { Name = "Clothing", Description = "Quần áo và phụ kiện" },
                    new Category { Name = "Books", Description = "Sách, tạp chí và tài liệu" },
                    new Category { Name = "Home & Garden", Description = "Đồ trang trí nhà cửa và làm vườn" },
                    new Category { Name = "Sports", Description = "Thiết bị thể thao và phụ kiện" },
                    new Category { Name = "Toys", Description = "Đồ chơi cho mọi lứa tuổi" },
                    new Category { Name = "Furniture", Description = "Đồ nội thất cho nhà ở và văn phòng" },
                    new Category { Name = "Beauty", Description = "Sản phẩm làm đẹp và mỹ phẩm" }
                };
                
                await context.Categories.AddRangeAsync(categories);
                await context.SaveChangesAsync();
            }
        }

        private static async Task<(List<ApplicationUser> vendors, List<ApplicationUser> customers)> SeedMultipleVendorsAndCustomers(UserManager<ApplicationUser> userManager, int vendorCount, int customerCount)
        {
            var vendors = new List<ApplicationUser>();
            var customers = new List<ApplicationUser>();

            for (int i = 1; i <= vendorCount; i++)
            {
                var email = $"vendor{i}@ecommerce.com";
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        FirstName = $"Vendor{i}",
                        LastName = "User",
                        CreatedAt = DateTime.UtcNow,
                        RegistrationDate = DateTime.UtcNow,
                        WalletBalance = 1_000_000m + i * 100_000m
                    };
                    var result = await userManager.CreateAsync(user, "Vendor@123456");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Vendor");
                    }
                }
                vendors.Add(user);
            }

            for (int i = 1; i <= customerCount; i++)
            {
                var email = $"customer{i}@ecommerce.com";
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        FirstName = $"Customer{i}",
                        LastName = "User",
                        CreatedAt = DateTime.UtcNow,
                        RegistrationDate = DateTime.UtcNow,
                        WalletBalance = 500_000m + i * 50_000m
                    };
                    var result = await userManager.CreateAsync(user, "Customer@123456");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Customer");
                    }
                }
                customers.Add(user);
            }

            return (vendors, customers);
        }

        private static async Task SeedProductsForVendors(ApplicationDbContext context, List<ApplicationUser> vendors, int productsPerVendor)
        {
            if (await context.Products.AnyAsync()) return;

            var categories = await context.Categories.OrderBy(c => c.Id).ToListAsync();
            var random = new Random();

            var products = new List<Product>();
            foreach (var v in vendors)
            {
                for (int i = 1; i <= productsPerVendor; i++)
                {
                    var cat = categories[random.Next(categories.Count)];
                    var name = i % 3 == 0 ? $"Điện thoại cũ {i}" : i % 3 == 1 ? $"Áo khoác second-hand {i}" : $"Sách cũ {i}";
                    var price = i % 3 == 0 ? random.Next(1_000_000, 8_000_000) : random.Next(50_000, 800_000);
                    products.Add(new Product
                    {
                        Name = name,
                        Description = "Hàng đã qua sử dụng, còn tốt.",
                        Price = price,
                        Quantity = 1,
                        CategoryId = cat.Id,
                        SellerId = v.Id,
                        Condition = (ProductCondition)random.Next(0, 5),
                        IsActive = true,
                        IsAvailable = true,
                        DateCreated = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                        DateListed = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }

        private static async Task SeedOrdersAndRelatedAsync(ApplicationDbContext context, List<ApplicationUser> vendors, List<ApplicationUser> customers, int orderCount)
        {
            if (await context.Orders.AnyAsync()) return;

            var random = new Random();
            var products = await context.Products.Where(p => p.IsActive && p.IsAvailable).ToListAsync();
            var orders = new List<Order>();

            for (int i = 0; i < orderCount && products.Any(); i++)
            {
                var product = products[random.Next(products.Count)];
                var customer = customers[random.Next(customers.Count)];
                var vendor = vendors.First(v => v.Id == product.SellerId);

                var status = (i % 10) switch
                {
                    0 => OrderStatus.Canceled,
                    1 => OrderStatus.Refunded,
                    2 => OrderStatus.Pending,
                    _ => OrderStatus.Accepted
                };

                var order = new Order
                {
                    UserId = customer.Id,
                    SellerId = vendor.Id,
                    OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6]}",
                    Status = status,
                    PaymentStatus = status == OrderStatus.Canceled ? PaymentStatus.Refunded : PaymentStatus.Paid,
                    TotalAmount = product.Price,
                    ShippingAddress = "123 Đường ABC",
                    ShippingCity = "Hồ Chí Minh",
                    ShippingCountry = "Việt Nam",
                    ShippingPostalCode = "700000",
                    PaymentMethod = "Wallet",
                    OrderDate = DateTime.UtcNow.AddDays(-random.Next(1, 20)),
                    UpdatedAt = DateTime.UtcNow
                };

                order.OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitPrice = product.Price,
                        Price = product.Price
                    }
                };

                // mark product sold if not pending
                if (status != OrderStatus.Pending)
                {
                    product.Quantity = 0;
                    product.IsAvailable = false;
                    product.IsActive = false;
                }

                orders.Add(order);
            }

            // Save orders first to generate IDs (and product updates)
            await context.Orders.AddRangeAsync(orders);
            await context.SaveChangesAsync();

            // Then create transactions and notifications referencing saved orders
            var transactions = new List<PaymentTransaction>();
            var notifications = new List<Notification>();

            foreach (var order in orders)
            {
                var t = new PaymentTransaction
                {
                    UserId = order.UserId,
                    Amount = order.TotalAmount,
                    Type = order.Status == OrderStatus.Refunded || order.Status == OrderStatus.Canceled ? TransactionType.Refund : TransactionType.Purchase,
                    Status = TransactionStatus.Completed,
                    TransactionReference = $"ORD-{order.OrderNumber}",
                    Description = order.Status == OrderStatus.Refunded || order.Status == OrderStatus.Canceled ? $"Hoàn tiền đơn hàng #{order.OrderNumber}" : $"Thanh toán đơn hàng #{order.OrderNumber}",
                    OrderId = order.Id,
                    PaymentMethod = "Wallet",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };
                transactions.Add(t);

                notifications.Add(new Notification
                {
                    UserId = order.SellerId!,
                    Title = "Đơn hàng mới",
                    Message = $"Bạn vừa nhận được đơn hàng #{order.OrderNumber}.",
                    Type = NotificationType.Order,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RelatedEntityId = order.Id.ToString(),
                    ActionUrl = $"/Vendor/Orders/Details/{order.Id}"
                });
            }

            await context.PaymentTransactions.AddRangeAsync(transactions);
            await context.Notifications.AddRangeAsync(notifications);
            await context.SaveChangesAsync();
        }

        private static async Task SeedStatisticsAsync(ApplicationDbContext context)
        {
            if (!await context.SystemStatistics.AnyAsync())
            {
                var stats = new SystemStatistics
                {
                    TotalRevenue = 0m,
                    TotalUsers = await context.Users.CountAsync(),
                    TotalVendors = await context.Users.CountAsync(),
                    TotalCustomers = await context.Users.CountAsync(),
                    TotalProducts = await context.Products.CountAsync(),
                    TotalOrders = await context.Orders.CountAsync(),
                    TotalOrdersCompleted = await context.Orders.CountAsync(o => o.Status == OrderStatus.Accepted),
                    ActiveUsersLast30Days = await context.Users.CountAsync(),
                    RevenueLast30Days = await context.PaymentTransactions
                        .Where(t => t.CreatedAt >= DateTime.UtcNow.AddDays(-30) && t.Status == TransactionStatus.Completed)
                        .SumAsync(t => (decimal?)t.Amount) ?? 0m,
                    NewUsersLast30Days = await context.Users.CountAsync(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-30)),
                    LastUpdated = DateTime.UtcNow,
                    MonthlyRevenue = Enumerable.Repeat(0m, 12).ToArray(),
                    CategoryNames = (await context.Categories.Select(c => c.Name).ToListAsync()).ToArray(),
                    ProductsPerCategory = (await context.Categories.Select(c => c.Products.Count).ToListAsync()).ToArray()
                };
                await context.SystemStatistics.AddAsync(stats);
            }

            if (!await context.UserStatistics.AnyAsync())
            {
                var users = await context.Users.ToListAsync();
                foreach (var u in users)
                {
                    var us = new UserStatistics
                    {
                        UserId = u.Id,
                        OrdersCount = await context.Orders.CountAsync(o => o.UserId == u.Id),
                        TotalProductsListed = await context.Products.CountAsync(p => p.SellerId == u.Id),
                        TotalProductsSold = 0,
                        TotalPurchases = 0,
                        TotalSalesAmount = 0m,
                        TotalPurchasesAmount = 0m,
                        TotalReviewsReceived = await context.Reviews.CountAsync(r => r.UserId == u.Id),
                        AverageRatingReceived = 0,
                        LastUpdated = DateTime.UtcNow,
                        MonthlySpending = Enumerable.Repeat(0m, 12).ToArray(),
                        MonthlyRevenue = Enumerable.Repeat(0m, 12).ToArray()
                    };
                    await context.UserStatistics.AddAsync(us);
                }
            }

            await context.SaveChangesAsync();
            
            // Seed recharge packages
            await RechargeDataSeeder.SeedRechargePackagesAsync(context);
        }
    }
}