# Tích hợp Mã Response VNPay vào Hệ thống

## Tóm tắt tích hợp

Hệ thống đã được tích hợp đầy đủ để xử lý tất cả các mã response từ VNPay với thông báo chi tiết cho người dùng.

## Các trường hợp xử lý

### ✅ **Thành công - Cộng tiền vào ví**

| Mã | Mô tả | Hành động | Thông báo |
|---|---|---|---|
| **00** | Giao dịch thành công | ✅ Cộng tiền vào ví | "✅ Giao dịch thành công - Số dư ví đã được cập nhật (+X₫)" |
| **07** | Trừ tiền thành công. Giao dịch bị nghi ngờ | ✅ Cộng tiền vào ví | "⚠️ Trừ tiền thành công. Giao dịch bị nghi ngờ - Số dư ví đã được cập nhật (+X₫)" |

### ❌ **Thất bại - Không cộng tiền**

| Mã | Mô tả | Hành động | Thông báo |
|---|---|---|---|
| **09** | Chưa đăng ký InternetBanking | ❌ Hủy giao dịch | "❌ Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng chưa đăng ký dịch vụ InternetBanking tại ngân hàng" |
| **10** | Xác thực sai quá 3 lần | ❌ Hủy giao dịch | "❌ Giao dịch không thành công do: Khách hàng xác thực thông tin thẻ/tài khoản không đúng quá 3 lần" |
| **11** | Hết hạn chờ thanh toán | ❌ Hủy giao dịch | "❌ Giao dịch không thành công do: Đã hết hạn chờ thanh toán. Xin quý khách vui lòng thực hiện lại giao dịch" |
| **12** | Thẻ/Tài khoản bị khóa | ❌ Hủy giao dịch | "❌ Giao dịch không thành công do: Thẻ/Tài khoản của khách hàng bị khóa" |
| **13** | Nhập sai OTP | ❌ Hủy giao dịch | "❌ Giao dịch không thành công do Quý khách nhập sai mật khẩu xác thực giao dịch (OTP). Xin quý khách vui lòng thực hiện lại giao dịch" |
| **24** | Khách hàng hủy giao dịch | ❌ Hủy giao dịch | "❌ Giao dịch không thành công do: Khách hàng hủy giao dịch" |
| **51** | Không đủ số dư | ❌ Hủy giao dịch | "❌ Giao dịch không thành công do: Tài khoản của quý khách không đủ số dư để thực hiện giao dịch" |
| **65** | Vượt hạn mức giao dịch | ❌ Hủy giao dịch | "❌ Giao dịch không thành công do: Tài khoản của Quý khách đã vượt quá hạn mức giao dịch trong ngày" |
| **75** | Ngân hàng bảo trì | ❌ Hủy giao dịch | "❌ Ngân hàng thanh toán đang bảo trì" |
| **79** | Nhập sai mật khẩu quá số lần | ❌ Hủy giao dịch | "❌ Giao dịch không thành công do: KH nhập sai mật khẩu thanh toán quá số lần quy định. Xin quý khách vui lòng thực hiện lại giao dịch" |
| **99** | Lỗi khác | ❌ Hủy giao dịch | "❌ Các lỗi khác (lỗi còn lại, không có trong danh sách mã lỗi đã liệt kê)" |

## Các file đã được tạo/cập nhật

### 1. **Services/VnPayResponseService.cs** (Mới)

- Xử lý tất cả mã response VNPay
- Trả về kết quả với thông báo chi tiết
- Xác định có nên cộng tiền vào ví hay không

### 2. **Controllers/RechargeController.cs** (Cập nhật)

- `PaymentCallbackVnpay()` action được cập nhật
- Sử dụng `VnPayResponseService` để xử lý response
- Hiển thị thông báo phù hợp (Success/Warning/Error)

### 3. **Views/Recharge/Index.cshtml** (Cập nhật)

- Thêm hiển thị thông báo với 3 loại:
  - **Success**: Màu xanh với icon ✅
  - **Warning**: Màu vàng với icon ⚠️  
  - **Error**: Màu đỏ với icon ❌

## Luồng xử lý

1. **User nạp tiền** → Tạo giao dịch → Chuyển đến VNPay
2. **VNPay xử lý** → Trả về callback với mã response
3. **Hệ thống nhận callback** → `VnPayResponseService.ProcessResponse()`
4. **Xử lý kết quả**:
   - Nếu `ShouldUpdateWallet = true` → Cộng tiền vào ví + Hiển thị thông báo thành công
   - Nếu `ShouldUpdateWallet = false` → Hủy giao dịch + Hiển thị thông báo lỗi chi tiết

## Kết quả

✅ **Tích hợp hoàn chỉnh** - Hệ thống xử lý tất cả mã response VNPay  
✅ **Thông báo chi tiết** - Người dùng biết chính xác lý do thành công/thất bại  
✅ **Bảo mật** - Chỉ cộng tiền khi giao dịch thực sự thành công  
✅ **UX tốt** - Thông báo rõ ràng với icon và màu sắc phù hợp  

## Test case với kết quả thực tế

Với kết quả giao dịch bạn cung cấp:

```json
{
  "vnPayResponseCode": "24",
  "success": true
}
```

**Kết quả**: Hệ thống sẽ hiển thị thông báo lỗi "❌ Giao dịch không thành công do: Khách hàng hủy giao dịch" và **KHÔNG** cộng tiền vào ví.
