using EcommerceSecondHand.Data;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSecondHand.Repositories
{
    public class SystemStatisticsRepository : Repository<SystemStatistics>, ISystemStatisticsRepository
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public SystemStatisticsRepository(ApplicationDbContext context, UserManager<ApplicationUser> userManager) 
            : base(context)
        {
            _userManager = userManager;
        }

        public async Task<SystemStatistics> GetCurrentStatisticsAsync()
        {
            var stats = await _dbSet.OrderByDescending(s => s.LastUpdated).FirstOrDefaultAsync();
            
            if (stats == null)
            {
                stats = new SystemStatistics();
                await _dbSet.AddAsync(stats);
                await _context.SaveChangesAsync();
            }
            
            return stats;
        }

        public async Task UpdateSystemStatisticsAsync()
        {
            var stats = await GetCurrentStatisticsAsync();

            // Calculate all statistics
            stats.TotalRevenue = await GetTotalRevenueAsync();
            stats.TotalUsers = await GetTotalUsersAsync();
            
            // Count by role
            var vendorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Vendor");
            var customerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Customer");
            
            if (vendorRole != null)
            {
                stats.TotalVendors = await _context.UserRoles
                    .CountAsync(ur => ur.RoleId == vendorRole.Id);
            }
            
            if (customerRole != null)
            {
                stats.TotalCustomers = await _context.UserRoles
                    .CountAsync(ur => ur.RoleId == customerRole.Id);
            }
            
            // Products, orders stats
            stats.TotalProducts = await _context.Products.CountAsync();
            stats.TotalOrders = await _context.Orders.CountAsync();
            stats.TotalOrdersCompleted = await _context.Orders
                .CountAsync(o => o.Status == OrderStatus.Accepted);
            
            // Last 30 days stats
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            
            stats.ActiveUsersLast30Days = await _context.Users
                .CountAsync(u => u.LastLoginDate >= thirtyDaysAgo);
            
            stats.RevenueLast30Days = await _context.Orders
                .Where(o => o.OrderDate >= thirtyDaysAgo && 
                           (o.Status == OrderStatus.Accepted))
                .SumAsync(o => o.TotalAmount);
            
            stats.NewUsersLast30Days = await _context.Users
                .CountAsync(u => u.RegistrationDate >= thirtyDaysAgo);
            
            stats.LastUpdated = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
        }

        public async Task<decimal> GetTotalRevenueAsync()
        {
            return await _context.Orders
                .Where(o => o.Status == OrderStatus.Accepted)
                .SumAsync(o => o.TotalAmount);
        }

        public async Task<int> GetTotalUsersAsync()
        {
            return await _context.Users.CountAsync();
        }

        public async Task<int> GetActiveUsersInPeriodAsync(int days)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            return await _context.Users
                .CountAsync(u => u.LastLoginDate >= cutoffDate);
        }

        public async Task<decimal> GetRevenueForPeriodAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Orders
                .Where(o => o.OrderDate >= startDate && 
                           o.OrderDate <= endDate && 
                           (o.Status == OrderStatus.Accepted))
                .SumAsync(o => o.TotalAmount);
        }

        public async Task<SystemStatistics> GetStatisticsAsync()
        {
            // First check if we have statistics that are recently updated (within the last hour)
            var recentStats = await _dbSet
                .OrderByDescending(s => s.LastUpdated)
                .FirstOrDefaultAsync(s => s.LastUpdated >= DateTime.UtcNow.AddHours(-1));
                
            if (recentStats != null)
            {
                return recentStats;
            }
            
            // If no recent stats, update and return
            await UpdateSystemStatisticsAsync();
            return await GetCurrentStatisticsAsync();
        }
    }
}