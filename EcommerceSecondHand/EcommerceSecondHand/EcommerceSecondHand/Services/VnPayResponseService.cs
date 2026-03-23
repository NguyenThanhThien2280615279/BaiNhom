using EcommerceSecondHand.Models;

namespace EcommerceSecondHand.Services
{
    public class VnPayResponseService
    {
        public static VnPayResponseResult ProcessResponse(string responseCode, string orderId, decimal amount)
        {
            var result = new VnPayResponseResult
            {
                OrderId = orderId,
                Amount = amount,
                ResponseCode = responseCode
            };

            switch (responseCode)
            {
                case "00":
                    result.Success = true;
                    result.Message = "Giao dịch thành công";
                    result.ShouldUpdateWallet = true;
                    break;

                case "07":
                    result.Success = true;
                    result.Message = "Trừ tiền thành công. Giao dịch bị nghi ngờ (liên quan tới lừa đảo, giao dịch bất thường)";
                    result.ShouldUpdateWallet = true;
                    result.IsSuspicious = true;
                    break;

                case "09":
                    result.Success = false;
                    result.Message = "Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng chưa đăng ký dịch vụ InternetBanking tại ngân hàng";
                    result.ShouldUpdateWallet = false;
                    break;

                case "10":
                    result.Success = false;
                    result.Message = "Giao dịch không thành công do: Khách hàng xác thực thông tin thẻ/tài khoản không đúng quá 3 lần";
                    result.ShouldUpdateWallet = false;
                    break;

                case "11":
                    result.Success = false;
                    result.Message = "Giao dịch không thành công do: Đã hết hạn chờ thanh toán. Xin quý khách vui lòng thực hiện lại giao dịch";
                    result.ShouldUpdateWallet = false;
                    break;

                case "12":
                    result.Success = false;
                    result.Message = "Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng bị khóa";
                    result.ShouldUpdateWallet = false;
                    break;

                case "13":
                    result.Success = false;
                    result.Message = "Giao dịch không thành công do Quý khách nhập sai mật khẩu xác thực giao dịch (OTP). Xin quý khách vui lòng thực hiện lại giao dịch";
                    result.ShouldUpdateWallet = false;
                    break;

                case "24":
                    result.Success = false;
                    result.Message = "Giao dịch không thành công do: Khách hàng hủy giao dịch";
                    result.ShouldUpdateWallet = false;
                    break;

                case "51":
                    result.Success = false;
                    result.Message = "Giao dịch không thành công do: Tài khoản của quý khách không đủ số dư để thực hiện giao dịch";
                    result.ShouldUpdateWallet = false;
                    break;

                case "65":
                    result.Success = false;
                    result.Message = "Giao dịch không thành công do: Tài khoản của Quý khách đã vượt quá hạn mức giao dịch trong ngày";
                    result.ShouldUpdateWallet = false;
                    break;

                case "75":
                    result.Success = false;
                    result.Message = "Ngân hàng thanh toán đang bảo trì";
                    result.ShouldUpdateWallet = false;
                    break;

                case "79":
                    result.Success = false;
                    result.Message = "Giao dịch không thành công do: KH nhập sai mật khẩu thanh toán quá số lần quy định. Xin quý khách vui lòng thực hiện lại giao dịch";
                    result.ShouldUpdateWallet = false;
                    break;

                case "99":
                    result.Success = false;
                    result.Message = "Các lỗi khác (lỗi còn lại, không có trong danh sách mã lỗi đã liệt kê)";
                    result.ShouldUpdateWallet = false;
                    break;

                default:
                    result.Success = false;
                    result.Message = $"Mã lỗi không xác định: {responseCode}";
                    result.ShouldUpdateWallet = false;
                    break;
            }

            return result;
        }
    }

    public class VnPayResponseResult
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string ResponseCode { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool ShouldUpdateWallet { get; set; }
        public bool IsSuspicious { get; set; } = false;
    }
}
