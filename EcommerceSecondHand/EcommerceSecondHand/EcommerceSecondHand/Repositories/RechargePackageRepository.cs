using EcommerceSecondHand.Data;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSecondHand.Repositories
{
    public class RechargePackageRepository : Repository<RechargePackage>, IRechargePackageRepository
    {
        public RechargePackageRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<RechargePackage>> GetActivePackagesAsync()
        {
            return await _dbSet
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Amount)
                .ToListAsync();
        }

        public async Task<RechargePackage?> GetByIdAsync(int id)
        {
            return await _dbSet
                .Where(p => p.Id == id && p.IsActive)
                .FirstOrDefaultAsync();
        }
    }
}
