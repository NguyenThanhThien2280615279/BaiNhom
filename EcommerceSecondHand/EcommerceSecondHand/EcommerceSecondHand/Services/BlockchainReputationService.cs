using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;

using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;

using EcommerceSecondHand.Data;          // <--- THÊM
using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Services
{
    public class BlockchainReputationService
    {
        private readonly Web3? _web3;
        private readonly string? _contractAddress;
        private readonly Contract? _contract;
        private readonly ILogger<BlockchainReputationService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db; // <--- THÊM

        // Nếu false => đang chạy chế độ mock/offline (không cần blockchain thật)
        private readonly bool _enabled;

        // ABI của smart contract ReputationRegistry
        private const string ContractAbi = @"[
            {""inputs"":[],""stateMutability"":""nonpayable"",""type"":""constructor""},
            {""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""seller"",""type"":""address""},{""indexed"":false,""internalType"":""bytes32"",""name"":""orderHash"",""type"":""bytes32""},{""indexed"":false,""internalType"":""uint256"",""name"":""newSuccessfulSales"",""type"":""uint256""}],""name"":""OrderRecorded"",""type"":""event""},
            {""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""seller"",""type"":""address""},{""indexed"":false,""internalType"":""uint8"",""name"":""stars"",""type"":""uint8""},{""indexed"":false,""internalType"":""uint256"",""name"":""newTotalStars"",""type"":""uint256""},{""indexed"":false,""internalType"":""uint256"",""name"":""newTotalReviews"",""type"":""uint256""}],""name"":""ReviewRecorded"",""type"":""event""},
            {""inputs"":[{""internalType"":""address"",""name"":""seller"",""type"":""address""},{""internalType"":""bytes32"",""name"":""orderHash"",""type"":""bytes32""}],""name"":""recordSuccessfulOrder"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""internalType"":""address"",""name"":""seller"",""type"":""address""},{""internalType"":""uint8"",""name"":""stars"",""type"":""uint8""}],""name"":""recordReview"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""internalType"":""address"",""name"":""seller"",""type"":""address""}],""name"":""getReputation"",""outputs"":[{""internalType"":""uint256"",""name"":""successfulSales"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""totalStars"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""totalReviews"",""type"":""uint256""},{""internalType"":""uint256"",""name"":""averageTimes100"",""type"":""uint256""},{""internalType"":""uint8"",""name"":""levelBadge"",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""}
        ]";

        public BlockchainReputationService(
            IConfiguration config,
            ILogger<BlockchainReputationService> logger,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db // <--- THÊM
        )
        {
            _logger = logger;
            _userManager = userManager;
            _db = db; // <--- THÊM

            // đọc config từ appsettings.json
            var rpcUrl = config["Blockchain:RpcUrl"];
            var privateKey = config["Blockchain:AdminPrivateKey"];
            _contractAddress = config["Blockchain:ContractAddress"];

            // Điều kiện để bật blockchain thật:
            // 1. Có rpcUrl
            // 2. Có privateKey thật (không phải placeholder)
            // 3. Có contractAddress
            // 4. privateKey KHÔNG chứa "YOUR_PRIVATE_KEY"
            var looksConfigured =
                !string.IsNullOrWhiteSpace(rpcUrl) &&
                !string.IsNullOrWhiteSpace(privateKey) &&
                !string.IsNullOrWhiteSpace(_contractAddress) &&
                !privateKey.Contains("YOUR_PRIVATE_KEY", StringComparison.OrdinalIgnoreCase);

            if (!looksConfigured)
            {
                _enabled = false;
                _logger.LogWarning("BlockchainReputationService DISABLED (missing or dummy config). Running in mock/offline mode.");
                return;
            }

            _enabled = true;

            try
            {
                // Nethereum: cần privateKey hợp lệ dạng hex
                var account = new Account(privateKey);
                _web3 = new Web3(account, rpcUrl);
                _contract = _web3.Eth.GetContract(ContractAbi, _contractAddress);
            }
            catch (Exception ex)
            {
                // nếu init web3 fail -> fallback mock
                _enabled = false;
                _logger.LogError(ex, "Init Web3/Contract failed. Fallback to mock/offline mode.");
            }
        }

        // hash chuỗi OrderNumber thành bytes32 để log on-chain
        private static byte[] Sha3Bytes(string input)
        {
            // Tạo Keccak-256 hash dạng hex string ("0xabc123...")
            var hashHex = Sha3Keccack.Current.CalculateHash(input);

            // Đổi hex string -> byte[]
            return hashHex.HexToByteArray();
        }

        // ============================================================
        // (1) GỌI KHI KHÁCH BẤM "XÁC NHẬN ĐÃ NHẬN HÀNG" (ConfirmDelivery)
        //
        // - Nếu _enabled == false (mock):
        //   * đảm bảo seller có BlockchainAddress giả
        //   * tăng uy tín local
        //   * lưu seller
        //   * GHI OnChainLog vào DB (đây là dữ liệu ví demo)
        //
        // - Nếu _enabled == true (live):
        //   * gọi contract thật
        //   * tạo log với txHash thật
        //   * sync lại từ chain
        // ============================================================
        public async Task RecordSuccessfulOrderAndSyncCacheAsync(Order order, ApplicationUser seller)
        {
            if (order == null)
            {
                _logger.LogWarning("RecordSuccessfulOrderAndSyncCacheAsync: order == null");
                return;
            }

            if (seller == null)
            {
                _logger.LogWarning("RecordSuccessfulOrderAndSyncCacheAsync: seller == null for order {orderNo}", order.OrderNumber);
                return;
            }

            // 1. Đảm bảo seller có địa chỉ ví. Nếu chưa có -> tạo ví giả.
            if (string.IsNullOrEmpty(seller.BlockchainAddress))
            {
                // Tạo chuỗi hex ngẫu nhiên (40 ký tự) dựa trên Guid
                // Guid.NewGuid().ToString("N") -> 32 ký tự hex
                var rnd = Guid.NewGuid().ToString("N"); // 32 hex
                var padded = (rnd + "0000000000000000000000000000000000000000")
                                .Substring(0, 40);
                seller.BlockchainAddress = "0x" + padded;
            }

            // ---------------- MOCK MODE / OFFLINE ----------------
            if (!_enabled)
            {
                _logger.LogInformation(
                    "Blockchain disabled. Auto-verifying seller {sellerId} locally.",
                    seller.Id
                );

                // Cộng số đơn giao thành công (không để âm)
                seller.OnChainSuccessfulSales = Math.Max(0, seller.OnChainSuccessfulSales) + 1;

                // Nếu chưa có rating thì set mặc định 5.0
                if (seller.OnChainAverageRating <= 0)
                {
                    seller.OnChainAverageRating = 5.0;
                }

                // Nếu chưa có badge thì set Bronze (1)
                if (seller.OnChainBadgeLevel == 0)
                {
                    seller.OnChainBadgeLevel = 1;
                }

                // Cập nhật badge theo rule local
                UpdateLocalBadgeEstimate(seller);

                // Lưu seller cập nhật uy tín
                await _userManager.UpdateAsync(seller);

                // --- Ghi OnChainLog (demo) vào DB ---
                var log = new OnChainLog
                {
                    Address = seller.BlockchainAddress,
                    EventType = "OrderRecorded",
                    OrderNumber = order.OrderNumber,
                    TxHash = $"mock-{Guid.NewGuid():N}", // ví dụ "mock-0ab12c..."
                    Details = $"Mock: order {order.OrderNumber} recorded for seller {seller.Id}",
                    Amount = order.TotalAmount,
                    CreatedAt = DateTime.UtcNow
                };

                _db.OnChainLogs.Add(log);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "Seller {sellerId} after confirm: addr={addr}, sales={sales}, rating={rating}, badge={badge}, logId={logId}",
                    seller.Id,
                    seller.BlockchainAddress,
                    seller.OnChainSuccessfulSales,
                    seller.OnChainAverageRating,
                    seller.OnChainBadgeLevel,
                    log.Id
                );

                return;
            }

            // ---------------- LIVE MODE (kết nối blockchain thật) ----------------
            if (_web3 == null || _contract == null)
            {
                _logger.LogError("Blockchain enabled but _web3/_contract is null. Skipping on-chain tx.");
                return;
            }

            var fn = _contract.GetFunction("recordSuccessfulOrder");

            var txHash = await fn.SendTransactionAsync(
                from: _web3.TransactionManager.Account.Address,
                gas: null,
                value: null,
                functionInput: new object[]
                {
                    seller.BlockchainAddress,
                    Sha3Bytes(order.OrderNumber)
                }
            );

            _logger.LogInformation("recordSuccessfulOrder tx sent: {tx}", txHash);

            // Ghi log live vào DB để ví demo vẫn thấy
            var liveLog = new OnChainLog
            {
                Address = seller.BlockchainAddress,
                EventType = "OrderRecorded",
                OrderNumber = order.OrderNumber,
                TxHash = txHash,
                Details = $"LIVE: order {order.OrderNumber} recorded for seller {seller.Id}",
                Amount = order.TotalAmount,
                CreatedAt = DateTime.UtcNow
            };

            _db.OnChainLogs.Add(liveLog);
            await _db.SaveChangesAsync();

            // Sau khi gửi tx thật, đọc lại từ on-chain để đồng bộ vào DB user
            await RefreshSellerOnChainCacheAsync(seller);
        }

        // ============================================================
        // (2) GỌI SAU KHI ADMIN DUYỆT REVIEW
        //
        // MOCK:
        //  - chỉ update rating local + badge
        //  - ghi OnChainLog "ReviewRecorded"
        //
        // LIVE:
        //  - gọi recordReview trên contract
        //  - sync lại seller
        //  - ghi OnChainLog với txHash thật
        // ============================================================
        public async Task RecordReviewAndSyncCacheAsync(ApplicationUser seller, int stars)
        {
            if (seller == null)
            {
                _logger.LogInformation("RecordReviewAndSyncCacheAsync: seller == null");
                return;
            }

            // đảm bảo seller có ví (cần cho log demo)
            if (string.IsNullOrEmpty(seller.BlockchainAddress))
            {
                var rnd = Guid.NewGuid().ToString("N");
                var padded = (rnd + "0000000000000000000000000000000000000000").Substring(0, 40);
                seller.BlockchainAddress = "0x" + padded;
            }

            // ---------------- MOCK MODE ----------------
            if (!_enabled)
            {
                _logger.LogInformation("Blockchain disabled. Mock update rating for seller {sellerId}", seller.Id);

                // Rating tạm kiểu trung bình đơn giản
                if (seller.OnChainAverageRating <= 0)
                {
                    seller.OnChainAverageRating = stars;
                }
                else
                {
                    seller.OnChainAverageRating = (seller.OnChainAverageRating + stars) / 2.0;
                }

                UpdateLocalBadgeEstimate(seller);

                await _userManager.UpdateAsync(seller);

                // Ghi log review mock
                var reviewLog = new OnChainLog
                {
                    Address = seller.BlockchainAddress,
                    EventType = "ReviewRecorded",
                    OrderNumber = null,
                    TxHash = $"mock-{Guid.NewGuid():N}",
                    Details = $"Mock review {stars}★ for seller {seller.Id}",
                    Amount = null,
                    CreatedAt = DateTime.UtcNow
                };

                _db.OnChainLogs.Add(reviewLog);
                await _db.SaveChangesAsync();

                return;
            }

            // ---------------- LIVE MODE ----------------
            if (_web3 == null || _contract == null)
            {
                _logger.LogError("Blockchain enabled but _web3/_contract is null. Skipping review transaction.");
                return;
            }

            var fn = _contract.GetFunction("recordReview");
            var txHash = await fn.SendTransactionAsync(
                from: _web3.TransactionManager.Account.Address,
                gas: null,
                value: null,
                functionInput: new object[]
                {
                    seller.BlockchainAddress,
                    (byte)stars
                }
            );

            _logger.LogInformation("recordReview tx: {tx}", txHash);

            // Lưu log review live
            var liveReviewLog = new OnChainLog
            {
                Address = seller.BlockchainAddress,
                EventType = "ReviewRecorded",
                OrderNumber = null,
                TxHash = txHash,
                Details = $"LIVE review {stars}★ for seller {seller.Id}",
                Amount = null,
                CreatedAt = DateTime.UtcNow
            };

            _db.OnChainLogs.Add(liveReviewLog);
            await _db.SaveChangesAsync();

            await RefreshSellerOnChainCacheAsync(seller);
        }

        // ============================================================
        // (3) ĐỌC LẠI DỮ LIỆU ON-CHAIN -> LƯU VỀ DB
        //
        // MOCK mode:
        //  - chỉ tính badge local và lưu
        //
        // LIVE mode:
        //  - gọi getReputation trong contract để cập nhật seller
        // ============================================================
        public async Task RefreshSellerOnChainCacheAsync(ApplicationUser seller)
        {
            if (seller == null) return;
            if (string.IsNullOrEmpty(seller.BlockchainAddress))
                return;

            if (!_enabled)
            {
                // MOCK MODE: chỉ đảm bảo badge hợp lý rồi lưu
                _logger.LogInformation("Blockchain disabled. Using local badge estimate for seller {sellerId}", seller.Id);

                UpdateLocalBadgeEstimate(seller);

                await _userManager.UpdateAsync(seller);
                return;
            }

            // LIVE MODE
            if (_contract == null)
            {
                _logger.LogError("Blockchain enabled but _contract is null in RefreshSellerOnChainCacheAsync.");
                return;
            }

            var fn = _contract.GetFunction("getReputation");

            var result = await fn.CallDeserializingToObjectAsync<GetRepOutputDTO>(
                seller.BlockchainAddress
            );

            seller.OnChainSuccessfulSales = (int)result.successfulSales;
            seller.OnChainAverageRating = (double)result.averageTimes100 / 100.0;
            seller.OnChainBadgeLevel = result.levelBadge;

            await _userManager.UpdateAsync(seller);
        }

        // ============================================================
        // RULE TẠM ĐỂ ƯỚC LƯỢNG BADGE KHI MOCK/OFFLINE
        //
        // Bronze  (1): >=1 đơn thành công và rating >=3.5
        // Silver  (2): >=5 đơn thành công và rating >=4.0
        // Gold    (3): >=20 đơn thành công và rating >=4.5
        //
        // => nếu seller mới bán 1 đơn, rating 5.0
        //    => sẽ nhảy Bronze => UI hiện "ĐÃ XÁC THỰC ✅"
        // ============================================================
        private static void UpdateLocalBadgeEstimate(ApplicationUser seller)
        {
            byte badge = seller.OnChainBadgeLevel; // giữ badge hiện tại làm default

            if (seller.OnChainSuccessfulSales >= 1 && seller.OnChainAverageRating >= 3.5)
                badge = Math.Max(badge, (byte)1); // Bronze

            if (seller.OnChainSuccessfulSales >= 5 && seller.OnChainAverageRating >= 4.0)
                badge = Math.Max(badge, (byte)2); // Silver

            if (seller.OnChainSuccessfulSales >= 20 && seller.OnChainAverageRating >= 4.5)
                badge = Math.Max(badge, (byte)3); // Gold

            seller.OnChainBadgeLevel = badge;
        }

        // DTO để đọc kết quả getReputation trong chế độ LIVE
        public class GetRepOutputDTO
        {
            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256", "successfulSales", 1)]
            public BigInteger successfulSales { get; set; }

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256", "totalStars", 2)]
            public BigInteger totalStars { get; set; }

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256", "totalReviews", 3)]
            public BigInteger totalReviews { get; set; }

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256", "averageTimes100", 4)]
            public BigInteger averageTimes100 { get; set; }

            [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint8", "levelBadge", 5)]
            public byte levelBadge { get; set; }
        }
    }
}
