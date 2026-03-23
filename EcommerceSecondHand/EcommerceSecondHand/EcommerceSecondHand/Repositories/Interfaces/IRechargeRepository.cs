using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Repositories.Interfaces
{
    public interface IRechargePackageRepository : IRepository<RechargePackage>
    {
        Task<IEnumerable<RechargePackage>> GetActivePackagesAsync();
        Task<RechargePackage?> GetByIdAsync(int id);
    }
    
    public interface IRechargeTransactionRepository : IRepository<RechargeTransaction>
    {
        Task<RechargeTransaction?> GetByTransactionCodeAsync(string transactionCode);
        Task<RechargeTransaction?> GetByVnPayTransactionIdAsync(string vnPayTransactionId);
        Task<IEnumerable<RechargeTransaction>> GetUserTransactionsAsync(string userId, int page = 1, int pageSize = 10);
        Task<int> CountUserTransactionsAsync(string userId);
        Task<IEnumerable<RechargeTransaction>> GetPendingTransactionsAsync();
        Task<RechargeTransaction> CreateTransactionAsync(string userId, int packageId);
        Task<bool> CompleteTransactionAsync(int transactionId, string vnPayTransactionId, string responseCode, string responseMessage);
        Task<bool> CancelTransactionAsync(int transactionId);
    }
}
