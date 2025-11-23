using DoAnChuyenNganh.Libraries;
using DoAnChuyenNganh.Models;
using DoAnChuyenNganh.Models.VnPay;
using DoAnChuyenNganh.Services.VnPay;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DoAnChuyenNganh.Services
{
    public class PremiumService
    {
        private readonly IVnPayService _vnPayService;
        private readonly UserManager<User> _userManager;

        public PremiumService(IVnPayService vnPayService, UserManager<User> userManager)
        {
            _vnPayService = vnPayService;
            _userManager = userManager;
        }

        /// <summary>
        /// Tạo URL thanh toán VnPay cho gói Premium
        /// </summary>
        public string CreatePremiumPaymentUrl(User user, PremiumPackageModel package, HttpContext context)
        {
            var pay = new VnPayLibrary();

            // Tạo URL tuyệt đối để VnPay callback
            var urlCallBack = $"{context.Request.Scheme}://{context.Request.Host}/Premium/PaymentCallback";

            // Sử dụng long cho vnp_Amount (VNĐ * 100)
            pay.AddRequestData("vnp_Version", "2.1.0");
            pay.AddRequestData("vnp_Command", "pay");
            pay.AddRequestData("vnp_TmnCode", "J6ESPHTB");
            pay.AddRequestData("vnp_Amount", ((long)(package.Amount * 100)).ToString());
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", "VND");
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", "vn");

            // Gửi userName trong OrderInfo để callback biết nâng cấp cho ai
            pay.AddRequestData("vnp_OrderInfo", $"{user.UserName}|{package.Description}");
            pay.AddRequestData("vnp_OrderType", package.OrderType);
            pay.AddRequestData("vnp_ReturnUrl", urlCallBack);
            pay.AddRequestData("vnp_TxnRef", DateTime.Now.Ticks.ToString());

            // Tạo URL thanh toán
            var paymentUrl = pay.CreateRequestUrl(
                "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                "32474NEVXK67VU51M6KUZ082TINHYFI5" // HashSecret của bạn
            );

            return paymentUrl;
        }

        /// <summary>
        /// Nâng cấp tài khoản Premium
        /// </summary>
        public async Task<bool> UpgradeToPremiumAsync(User user, PremiumPackageModel package)
        {
            user.IsPremium = true;
            user.PremiumExpireDate = DateTime.Now.AddDays(package.DurationInDays);
            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }

        /// <summary>
        /// Xử lý callback từ VnPay
        /// </summary>
        public async Task<PaymentResponseModel> ExecuteVnPayCallback(IQueryCollection query)
        {
            // 1. Xác thực signature & lấy thông tin trả về
            var response = _vnPayService.PaymentExecute(query);

            if (!response.Success)
                return response; // Thanh toán thất bại hoặc signature sai

            // 2. Lấy userName từ vnp_OrderInfo
            var orderInfo = response.OrderDescription; // vnp_OrderInfo
            var parts = orderInfo.Split('|');
            var userName = parts[0];

            var user = await _userManager.FindByNameAsync(userName);
            if (user == null) return response; // User không tồn tại

            // 3. Chỉ nâng cấp nếu thanh toán thành công
            if (response.VnPayResponseCode == "00") // Mã 00 = thành công
            {
                var package = new PremiumPackageModel();
                await UpgradeToPremiumAsync(user, package);
            }

            return response;
        }
    }
}
