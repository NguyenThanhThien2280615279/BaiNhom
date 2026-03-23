using EcommerceSecondHand.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace EcommerceSecondHand.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _db;
        private readonly HttpClient _httpClient;

        public ChatbotController(IConfiguration config, ApplicationDbContext db)
        {
            _config = config;
            _db = db;
            _httpClient = new HttpClient();
        }

        [HttpPost]
        public async Task<IActionResult> Ask(string message)
        {
            // Kiểm tra đầu vào
            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { reply = "Bạn hãy nhập nội dung để hỏi nhé." });
            }

            // Lấy cấu hình
            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Json(new { reply = "Chưa cấu hình API key trong appsettings.json." });
            }

            // Kiểm tra mô hình (sử dụng mô hình mặc định nếu không có mô hình trong cấu hình)
            var model = _config["Gemini:Model"] ?? "gemini-2.5-flash"; // Nếu không có mô hình cấu hình thì dùng gemini-2.5-flash mặc định
            var url = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={apiKey}";

            try
            {
                // 1. Lấy danh sách sản phẩm từ cơ sở dữ liệu
                var products = await _db.Products
                    .Include(p => p.Category)
                    .OrderByDescending(p => p.Id)
                    .Take(30)
                    .ToListAsync();

                // 2. Chuyển danh sách sản phẩm thành văn bản
                var productInfo = new StringBuilder();
                if (products.Any())
                {
                    foreach (var p in products)
                    {
                        var categoryName = p.Category != null ? p.Category.Name : "Không rõ danh mục";
                        var priceText = p.Price.ToString("N0") + " đ";
                        productInfo.AppendLine($"- Id={p.Id}; Tên={p.Name}; Danh mục={categoryName}; Giá={priceText}; Tình trạng={p.Condition};");
                    }
                }
                else
                {
                    productInfo.AppendLine("- Hiện chưa có sản phẩm nào trong cửa hàng.");
                }

                // 3. Tạo Prompt tối ưu
                var prompt = "Bạn là trợ lý AI của website mua bán đồ cũ.\n" +
                             "Dưới đây là danh sách sản phẩm hiện có trong cửa hàng. Không được bịa thêm sản phẩm ngoài danh sách này.\n\n" +
                             productInfo.ToString() + "\n" +
                             "Nếu người dùng hỏi về mua hàng, hãy gợi ý 3 đến 5 sản phẩm phù hợp từ danh sách trên.\n" +
                             "Khi gợi ý, hãy nêu: Tên sản phẩm, Danh mục, Giá, Tình trạng.\n" +
                             "Trả lời bằng tiếng Việt dễ hiểu.\n" +
                             "Nếu câu hỏi không liên quan đến mua bán hoặc sản phẩm, hãy trả lời như một trợ lý AI bình thường.\n\n" +
                             "Câu hỏi của người dùng: " + message;

                // 4. Cấu trúc body yêu cầu
                var requestBody = new
                {
                    contents = new[] {
                        new {
                            parts = new[] {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 1000
                    }
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                // 5. Gửi yêu cầu đến API
                var response = await _httpClient.PostAsync(url, jsonContent);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Lỗi từ API
                    return Json(new { reply = "Lỗi API Gemini: " + responseString });
                }

                // 6. Xử lý kết quả trả về từ API
                using var doc = JsonDocument.Parse(responseString);

                // Trích xuất text từ kết quả trả về
                var reply = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return Json(new { reply = reply ?? "Xin lỗi, hiện mình chưa trả lời được câu này." });
            }
            catch (Exception ex)
            {
                // Xử lý lỗi hệ thống
                return Json(new { reply = "Lỗi hệ thống khi kết nối AI: " + ex.Message });
            }
        }
    }
}