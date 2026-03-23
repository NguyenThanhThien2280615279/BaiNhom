using EcommerceSecondHand.Data;
using EcommerceSecondHand.Models;
using EcommerceSecondHand.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EcommerceSecondHand.Repositories
{
    public class PaymentTransactionRepository : Repository<PaymentTransaction>, IPaymentTransactionRepository
    {
        public PaymentTransactionRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<PaymentTransaction>> GetUserTransactionsAsync(string userId)
        {
            return await _dbSet
                .Where(pt => pt.UserId == userId)
                .OrderByDescending(pt => pt.CreatedAt)
                .Include(pt => pt.Order)
                .ToListAsync();
        }

        public async Task<IEnumerable<PaymentTransaction>> GetUserTransactionsByTypeAsync(string userId, TransactionType type)
        {
            return await _dbSet
                .Where(pt => pt.UserId == userId && pt.Type == type)
                .OrderByDescending(pt => pt.CreatedAt)
                .Include(pt => pt.Order)
                .ToListAsync();
        }

        public async Task<IEnumerable<PaymentTransaction>> GetPaginatedTransactionsAsync(string userId, int page, int pageSize)
        {
            return await _dbSet
                .Where(pt => pt.UserId == userId)
                .OrderByDescending(pt => pt.CreatedAt)
                .Include(pt => pt.Order)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTransactionsCountAsync(string userId)
        {
            return await _dbSet
                .Where(pt => pt.UserId == userId)
                .CountAsync();
        }

        public async Task<PaymentTransaction?> GetLastTransactionAsync(string userId)
        {
            return await _dbSet
                .Where(pt => pt.UserId == userId)
                .OrderByDescending(pt => pt.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<decimal> GetTotalSpentAsync(string userId)
        {
            return await _dbSet
                .Where(pt => pt.UserId == userId && 
                       pt.Status == TransactionStatus.Completed && 
                       pt.Type == TransactionType.Purchase)
                .SumAsync(pt => pt.Amount);
        }

        public async Task UpdateAsync(PaymentTransaction transaction)
        {
            _context.Entry(transaction).State = EntityState.Modified;
            await SaveAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var transaction = await GetByIdAsync(id);
            if (transaction != null)
            {
                _dbSet.Remove(transaction);
                await SaveAsync();
            }
        }

        public async Task<IEnumerable<PaymentTransaction>> GetRecentTransactionsAsync(int count)
        {
            return await _dbSet
                .OrderByDescending(pt => pt.CreatedAt)
                .Include(pt => pt.User)
                .Include(pt => pt.Order)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<PaymentTransaction>> GetRecentTransactionsByUserAsync(string userId, int count)
        {
            return await _dbSet
                .Where(pt => pt.UserId == userId)
                .OrderByDescending(pt => pt.CreatedAt)
                .Include(pt => pt.Order)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<PaymentTransaction>> GetTransactionsByUserAsync(
            string userId, 
            string? type, 
            string? status, 
            string? sortOrder, 
            int page, 
            int pageSize)
        {
            var query = _dbSet.Where(pt => pt.UserId == userId);

            // Apply type filter
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, true, out var transactionType))
            {
                query = query.Where(pt => pt.Type == transactionType);
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<TransactionStatus>(status, true, out var transactionStatus))
            {
                query = query.Where(pt => pt.Status == transactionStatus);
            }

            // Apply sorting
            query = sortOrder switch
            {
                "oldest" => query.OrderBy(pt => pt.CreatedAt),
                "amountAsc" => query.OrderBy(pt => pt.Amount),
                "amountDesc" => query.OrderByDescending(pt => pt.Amount),
                _ => query.OrderByDescending(pt => pt.CreatedAt) // "newest" is default
            };

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(pt => pt.Order)
                .ToListAsync();
        }

        public async Task<int> CountTransactionsByUserAsync(
            string userId, 
            string? type, 
            string? status)
        {
            var query = _dbSet.Where(pt => pt.UserId == userId);

            // Apply type filter
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, true, out var transactionType))
            {
                query = query.Where(pt => pt.Type == transactionType);
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<TransactionStatus>(status, true, out var transactionStatus))
            {
                query = query.Where(pt => pt.Status == transactionStatus);
            }

            return await query.CountAsync();
        }

        public async Task<int> CountTransactionsByUserAsync(
            string userId, 
            string? type,
            DateTime? startDate,
            DateTime? endDate)
        {
            var query = _dbSet.Where(pt => pt.UserId == userId);

            // Apply type filter
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, true, out var transactionType))
            {
                query = query.Where(pt => pt.Type == transactionType);
            }

            // Apply date range filter
            if (startDate.HasValue)
            {
                query = query.Where(pt => pt.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(pt => pt.CreatedAt <= endDate.Value);
            }

            return await query.CountAsync();
        }

        public async Task<IEnumerable<PaymentTransaction>> GetPendingTransactionsAsync()
        {
            return await _dbSet
                .Where(pt => pt.Status == TransactionStatus.Pending)
                .OrderByDescending(pt => pt.CreatedAt)
                .Include(pt => pt.User)
                .ToListAsync();
        }

        public async Task<IEnumerable<PaymentTransaction>> GetFilteredTransactionsAsync(
            string? userId,
            string? type,
            string? status,
            DateTime? startDate,
            DateTime? endDate,
            int page,
            int pageSize)
        {
            var query = _dbSet.AsQueryable();

            // Apply userId filter
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(pt => pt.UserId == userId);
            }

            // Apply type filter
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, true, out var transactionType))
            {
                query = query.Where(pt => pt.Type == transactionType);
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<TransactionStatus>(status, true, out var transactionStatus))
            {
                query = query.Where(pt => pt.Status == transactionStatus);
            }

            // Apply date range filter
            if (startDate.HasValue)
            {
                query = query.Where(pt => pt.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(pt => pt.CreatedAt <= endDate.Value);
            }

            return await query
                .OrderByDescending(pt => pt.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(pt => pt.User)
                .Include(pt => pt.Order)
                .ToListAsync();
        }

        public async Task<int> CountFilteredTransactionsAsync(
            string? type,
            string? status,
            DateTime? startDate,
            DateTime? endDate)
        {
            var query = _dbSet.AsQueryable();

            // Apply type filter
            if (!string.IsNullOrEmpty(type) && Enum.TryParse<TransactionType>(type, true, out var transactionType))
            {
                query = query.Where(pt => pt.Type == transactionType);
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<TransactionStatus>(status, true, out var transactionStatus))
            {
                query = query.Where(pt => pt.Status == transactionStatus);
            }

            // Apply date range filter
            if (startDate.HasValue)
            {
                query = query.Where(pt => pt.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(pt => pt.CreatedAt <= endDate.Value);
            }

            return await query.CountAsync();
        }

        public async Task<decimal> GetTotalSystemBalanceAsync()
        {
            // The total system balance is the sum of all completed deposits minus the sum of all completed withdrawals
            var totalDeposits = await _dbSet
                .Where(pt => pt.Type == TransactionType.Deposit && pt.Status == TransactionStatus.Completed)
                .SumAsync(pt => pt.Amount);
                
            var totalWithdrawals = await _dbSet
                .Where(pt => pt.Type == TransactionType.Withdraw && pt.Status == TransactionStatus.Completed)
                .SumAsync(pt => pt.Amount);
                
            return totalDeposits - totalWithdrawals;
        }

        public async Task<IEnumerable<PaymentTransaction>> GetTransactionsByStatusAsync(TransactionStatus status)
        {
            return await _dbSet
                .Where(pt => pt.Status == status)
                .OrderByDescending(pt => pt.CreatedAt)
                .Include(pt => pt.User)
                .Include(pt => pt.Order)
                .ToListAsync();
        }

        public async Task<PaymentTransaction?> GetTransactionByReferenceAsync(string reference)
        {
            return await _dbSet
                .FirstOrDefaultAsync(pt => pt.TransactionReference == reference);
        }

        public async Task<decimal> GetTotalRevenueAsync()
        {
            return await _dbSet
                .Where(pt => pt.Status == TransactionStatus.Completed && 
                       (pt.Type == TransactionType.Purchase || pt.Type == TransactionType.Sale))
                .SumAsync(pt => pt.Amount);
        }

        public async Task<decimal> GetRevenueForPeriodAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbSet
                .Where(pt => pt.Status == TransactionStatus.Completed && 
                       (pt.Type == TransactionType.Purchase || pt.Type == TransactionType.Sale) &&
                       pt.CreatedAt >= startDate && pt.CreatedAt <= endDate)
                .SumAsync(pt => pt.Amount);
        }
        // ✅ Dán thêm phần này
        public async Task<List<PaymentTransaction>> GetRecentByUserAsync(string userId, int take)
        {
            return await _context.PaymentTransactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}