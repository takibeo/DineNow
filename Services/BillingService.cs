using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Libraries;
using DoAnChuyenNganh.Models;
using DoAnChuyenNganh.Models.VnPay;
using DoAnChuyenNganh.Services.VnPay;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Services
{
    public class BillingService
    {
        private readonly IVnPayService _vnPayService;
        private readonly UserManager<User> _userManager;
        private readonly AppDBContext _context;

        private const decimal FeePerRestaurant = 300000m;
        private const decimal FeePerReservation = 5000m;
        private const decimal AdminCommissionRate = 0.08m; 


        public BillingService(IVnPayService vnPayService, UserManager<User> userManager, AppDBContext context)
        {
            _vnPayService = vnPayService;
            _userManager = userManager;
            _context = context;
        }

        /// <summary>
        /// Tạo URL thanh toán VnPay cho 1 StaffBilling
        /// </summary>
        public string CreateBillingPaymentUrl(User user, StaffBilling bill, HttpContext context)
        {
            var pay = new VnPayLibrary();

            var urlCallBack = $"{context.Request.Scheme}://{context.Request.Host}/StaffBilling/PaymentCallback";

            pay.AddRequestData("vnp_Version", "2.1.0");
            pay.AddRequestData("vnp_Command", "pay");
            pay.AddRequestData("vnp_TmnCode", "J6ESPHTB"); // Thay bằng Terminal code của bạn
            pay.AddRequestData("vnp_Amount", ((long)(bill.TotalFee * 100)).ToString());
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", "VND");
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", "vn");

            // Gửi billId trong OrderInfo để callback biết Staff thanh toán bill nào
            pay.AddRequestData("vnp_OrderInfo", $"{user.UserName}|StaffBilling|{bill.Id}");
            pay.AddRequestData("vnp_OrderType", "billpayment");
            pay.AddRequestData("vnp_ReturnUrl", urlCallBack);
            pay.AddRequestData("vnp_TxnRef", DateTime.Now.Ticks.ToString());

            var paymentUrl = pay.CreateRequestUrl(
                "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                "32474NEVXK67VU51M6KUZ082TINHYFI5" // HashSecret của bạn
            );

            return paymentUrl;
        }

        /// <summary>
        /// Tạo hoặc tính phí hàng tháng cho Staff
        /// </summary>
        public async Task<StaffBilling> CalculateMonthlyFee(string staffId, int? year = null, int? month = null)
        {
            if (string.IsNullOrEmpty(staffId)) return null;

            int y = year ?? DateTime.Now.Year;
            int m = month ?? DateTime.Now.Month;

            try
            {
                // Kiểm tra bill đã tồn tại chưa
                var existingBill = await _context.StaffBillings
                    .FirstOrDefaultAsync(b => b.UserId == staffId && b.Month.Year == y && b.Month.Month == m);

                if (existingBill != null)
                    return existingBill; // Bill đã có, trả về luôn

                // Lấy các nhà hàng Staff quản lý
                var restaurantIds = await _context.StaffRestaurants
                    .Where(sr => sr.UserId == staffId)
                    .Select(sr => sr.RestaurantId)
                    .ToListAsync();

                if (!restaurantIds.Any())
                    return null; // Staff chưa quản lý nhà hàng nào

                var startDate = new DateTime(y, m, 1);
                var endDate = startDate.AddMonths(1);

                int reservationCount = await _context.Reservations
                    .Where(r => restaurantIds.Contains(r.RestaurantId)
                                && r.Status == "Confirmed"
                                && r.ReservationDate >= startDate
                                && r.ReservationDate < endDate)
                    .CountAsync();

                decimal restaurantFee = restaurantIds.Count * FeePerRestaurant;
                decimal reservationFee = reservationCount * FeePerReservation;
                decimal totalFee = restaurantFee + reservationFee;

                if (totalFee <= 0)
                    return null; // Không có phí → không tạo bill

                var bill = new StaffBilling
                {
                    UserId = staffId,
                    Month = startDate,
                    ManagedRestaurantCount = restaurantIds.Count,
                    ReservationCount = reservationCount,
                    RestaurantFee = restaurantFee,
                    ReservationFee = reservationFee,
                    TotalFee = totalFee,
                    Status = BillingStatus.Unpaid // Mặc định chưa thanh toán
                };

                _context.StaffBillings.Add(bill);
                await _context.SaveChangesAsync(); // Nếu duplicate → DbUpdateException

                return bill;
            }
            catch (DbUpdateException)
            {
                // Nếu insert bị duplicate key, lấy bill hiện có
                var bill = await _context.StaffBillings
                    .FirstOrDefaultAsync(b => b.UserId == staffId && b.Month.Year == y && b.Month.Month == m);
                return bill;
            }
        }

        /// <summary>
        /// Callback VnPay xử lý thanh toán
        /// </summary>
        public async Task<PaymentResponseModel> ExecuteVnPayCallback(IQueryCollection query)
        {
            var response = _vnPayService.PaymentExecute(query);

            if (!response.Success)
                return response;

            var orderInfo = response.OrderDescription; // vnp_OrderInfo
            var parts = orderInfo.Split('|');

            if (parts.Length != 3) return response;

            var userName = parts[0];
            var billId = int.Parse(parts[2]);

            var user = await _userManager.FindByNameAsync(userName);
            var bill = await _context.StaffBillings.FindAsync(billId);

            if (user == null || bill == null) return response;

            if (response.VnPayResponseCode == "00") // Thanh toán thành công
            {
                // Thay vì IsPaid = true, chuyển sang Pending
                bill.Status = BillingStatus.Pending;
                await _context.SaveChangesAsync();
            }

            return response;
        }

        public string CreateOrderPaymentUrl(User user, Order order, HttpContext context)
        {
            var pay = new VnPayLibrary();
            var urlCallBack = $"{context.Request.Scheme}://{context.Request.Host}/Order/PaymentCallback";

            pay.AddRequestData("vnp_Version", "2.1.0");
            pay.AddRequestData("vnp_Command", "pay");
            pay.AddRequestData("vnp_TmnCode", "J6ESPHTB"); // Thay bằng Terminal code của bạn
            pay.AddRequestData("vnp_Amount", ((long)(order.TotalAmount * 100)).ToString());
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            pay.AddRequestData("vnp_CurrCode", "VND");
            pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
            pay.AddRequestData("vnp_Locale", "vn");
            pay.AddRequestData("vnp_OrderInfo", $"{user.UserName}|Order|{order.Id}");
            pay.AddRequestData("vnp_OrderType", "billpayment");
            pay.AddRequestData("vnp_ReturnUrl", urlCallBack);
            pay.AddRequestData("vnp_TxnRef", DateTime.Now.Ticks.ToString());

            return pay.CreateRequestUrl(
                "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                "32474NEVXK67VU51M6KUZ082TINHYFI5"
            );
        }
        public async Task<PaymentResponseModel> ExecuteOrderVnPayCallback(IQueryCollection query)
        {
            var response = _vnPayService.PaymentExecute(query);
            if (!response.Success) return response;

            var parts = response.OrderDescription.Split('|');
            if (parts.Length != 3) return response;

            var userName = parts[0];
            var orderId = int.Parse(parts[2]);

            var user = await _userManager.FindByNameAsync(userName);
            var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);

            if (user == null || order == null) return response;

            if (response.VnPayResponseCode == "00") // Thanh toán thành công
            {
                order.Status = "Paid";
                await _context.SaveChangesAsync();
            }


            return response;
        }
    }
}
