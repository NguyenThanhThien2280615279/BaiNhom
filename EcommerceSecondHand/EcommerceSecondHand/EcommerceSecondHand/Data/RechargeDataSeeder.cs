using EcommerceSecondHand.Data;
using EcommerceSecondHand.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSecondHand.Data
{
    public static class RechargeDataSeeder
    {
        public static async Task SeedRechargePackagesAsync(ApplicationDbContext context)
        {
            if (await context.RechargePackages.AnyAsync())
                return;

            var packages = new List<RechargePackage>
            {
                new RechargePackage
                {
                    Name = "Gói Cơ Bản",
                    Description = "Nạp tiền cơ bản cho người mới",
                    Amount = 50000,
                    BonusAmount = 0,
                    IsActive = true,
                    SortOrder = 1
                },
                new RechargePackage
                {
                    Name = "Gói Tiết Kiệm",
                    Description = "Nạp 100k nhận thêm 5k",
                    Amount = 100000,
                    BonusAmount = 5000,
                    IsActive = true,
                    SortOrder = 2
                },
                new RechargePackage
                {
                    Name = "Gói Ưu Đãi",
                    Description = "Nạp 200k nhận thêm 20k",
                    Amount = 200000,
                    BonusAmount = 20000,
                    IsActive = true,
                    SortOrder = 3
                },
                new RechargePackage
                {
                    Name = "Gói VIP",
                    Description = "Nạp 500k nhận thêm 75k",
                    Amount = 500000,
                    BonusAmount = 75000,
                    IsActive = true,
                    SortOrder = 4
                },
                new RechargePackage
                {
                    Name = "Gói Premium",
                    Description = "Nạp 1 triệu nhận thêm 200k",
                    Amount = 1000000,
                    BonusAmount = 200000,
                    IsActive = true,
                    SortOrder = 5
                }
            };

            context.RechargePackages.AddRange(packages);
            await context.SaveChangesAsync();
        }
    }
}
