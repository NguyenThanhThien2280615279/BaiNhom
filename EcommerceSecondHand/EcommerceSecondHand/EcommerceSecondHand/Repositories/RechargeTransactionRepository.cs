using EcommerceSecondHand.Data;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSecondHand.Repositories
{
    public class RechargeTransactionRepository : Repository<RechargeTransaction>, IRechargeTransactionRepository
    {
        public RechargeTransactionRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<RechargeTransaction?> GetByTransactionCodeAsync(string transactionCode)
        {
            return await _dbSet
                .Include(t => t.User)
                .Include(t => t.RechargePackage)
                .FirstOrDefaultAsync(t => t.TransactionCode == transactionCode);
        }

        public async Task<RechargeTransaction?> GetByVnPayTransactionIdAsync(string vnPayTransactionId)
        {
            return await _dbSet
                .Include(t => t.User)
                .Include(t => t.RechargePackage)
                .FirstOrDefaultAsync(t => t.VnPayTransactionId == vnPayTransactionId);
        }

        public async Task<IEnumerable<RechargeTransaction>> GetUserTransactionsAsync(string userId, int page = 1, int pageSize = 10)
        {
            return await _dbSet
                .Where(t => t.UserId == userId)
                .Include(t => t.RechargePackage)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> CountUserTransactionsAsync(string userId)
        {
            return await _dbSet
                .Where(t => t.UserId == userId)
                .CountAsync();
        }

        public async Task<IEnumerable<RechargeTransaction>> GetPendingTransactionsAsync()
        {
            return await _dbSet
                .Where(t => t.Status == RechargeStatus.Pending || t.Status == RechargeStatus.Processing)
                .Include(t => t.User)
                .Include(t => t.RechargePackage)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<RechargeTransaction> CreateTransactionAsync(string userId, int packageId)
        {
            var package = await _context.RechargePackages.FindAsync(packageId);
            if (package == null)
                throw new ArgumentException("Gói nạp tiền không tồn tại");

            var transaction = new RechargeTransaction
            {
                UserId = userId,
                RechargePackageId = packageId,
                TransactionCode = GenerateTransactionCode(),
                Amount = package.Amount,
                BonusAmount = package.BonusAmount,
                TotalAmount = package.TotalAmount,
                Status = RechargeStatus.Pending,
                ExpiredAt = DateTime.UtcNow.AddMinutes(15) // Hết hạn sau 15 phút
            };

            _dbSet.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task<bool> CompleteTransactionAsync(int transactionId, string vnPayTransactionId, string responseCode, string responseMessage)
        {
            var transaction = await _dbSet
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null || transaction.Status != RechargeStatus.Pending)
                return false;

            transaction.Status = RechargeStatus.Completed;
            transaction.VnPayTransactionId = vnPayTransactionId;
            transaction.VnPayResponseCode = responseCode;
            transaction.VnPayResponseMessage = responseMessage;
            transaction.CompletedAt = DateTime.UtcNow;

            // Cập nhật số dư ví của user
            if (transaction.User != null)
            {
                transaction.User.WalletBalance += transaction.TotalAmount;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CancelTransactionAsync(int transactionId)
        {
            var transaction = await _dbSet.FindAsync(transactionId);
            if (transaction == null || transaction.Status != RechargeStatus.Pending)
                return false;

            transaction.Status = RechargeStatus.Canceled;
            await _context.SaveChangesAsync();
            return true;
        }

        private string GenerateTransactionCode()
        {
            return $"RCH{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        }
    }
}
