using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Repositories.Interfaces
{
    public interface IPaymentTransactionRepository : IRepository<PaymentTransaction>
    {
        Task<IEnumerable<PaymentTransaction>> GetUserTransactionsAsync(string userId);
        Task<IEnumerable<PaymentTransaction>> GetUserTransactionsByTypeAsync(string userId, TransactionType type);
        Task<IEnumerable<PaymentTransaction>> GetPaginatedTransactionsAsync(string userId, int page, int pageSize);
        Task<int> GetTransactionsCountAsync(string userId);
        Task<PaymentTransaction?> GetLastTransactionAsync(string userId);
        Task<decimal> GetTotalSpentAsync(string userId);
        Task<IEnumerable<PaymentTransaction>> GetRecentTransactionsAsync(int count);
        Task<IEnumerable<PaymentTransaction>> GetTransactionsByStatusAsync(TransactionStatus status);
        Task<PaymentTransaction?> GetTransactionByReferenceAsync(string reference);
        Task<decimal> GetTotalRevenueAsync();
        Task<decimal> GetRevenueForPeriodAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalSystemBalanceAsync();

        // ? Thêm method này ð? View g?i
        Task<List<PaymentTransaction>> GetRecentByUserAsync(string userId, int take);
        // Missing methods for Customer/Vendor controllers
        Task<IEnumerable<PaymentTransaction>> GetRecentTransactionsByUserAsync(string userId, int count);
        Task<IEnumerable<PaymentTransaction>> GetTransactionsByUserAsync(string userId, string? type = null, 
            string? status = null, string? sortOrder = null, int page = 1, int pageSize = 10);
        Task<int> CountTransactionsByUserAsync(string userId, string? type = null, string? status = null);
        Task<int> CountTransactionsByUserAsync(string userId, string? type, DateTime? startDate, DateTime? endDate);
        
        // Methods needed by AdminController
        Task<IEnumerable<PaymentTransaction>> GetPendingTransactionsAsync();
        Task<IEnumerable<PaymentTransaction>> GetFilteredTransactionsAsync(string? userId = null, string? type = null, 
            string? status = null, DateTime? startDate = null, DateTime? endDate = null, int page = 1, int pageSize = 10);
        Task<int> CountFilteredTransactionsAsync(string? type, string? status, DateTime? startDate, DateTime? endDate);
        
        new Task AddAsync(PaymentTransaction transaction);
        Task UpdateAsync(PaymentTransaction transaction);
        Task DeleteAsync(int id);
        new Task SaveAsync();
    }
}