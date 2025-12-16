using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using DoAnChuyenNganh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffBillingController : Controller
    {
        private readonly AppDBContext _context;
        private readonly UserManager<User> _userManager;
        private readonly BillingService _billingService;

        public StaffBillingController(AppDBContext context, UserManager<User> userManager, BillingService billingService)
        {
            _context = context;
            _userManager = userManager;
            _billingService = billingService;
        }

        private async Task SaveNotification(string userId, string message)
        {
            var sender = await _userManager.GetUserAsync(User);
            var phone = sender?.PhoneNumber ?? "Không có";

            message += $"\n\nMọi thắc mắc xin vui lòng liên hệ số điện thoại: {phone}";

            var noti = new Notification
            {
                UserId = userId,
                Message = message,
                Type = "System",
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(noti);
            await _context.SaveChangesAsync();
        }

        // GET: StaffBilling/Index
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var bills = await _context.StaffBillings
                .Where(b => b.UserId == user.Id)
                .OrderByDescending(b => b.Month)
                .ToListAsync();

            return View(bills);
        }

        // GET: StaffBilling/Pay/5
        public async Task<IActionResult> Pay(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var bill = await _context.StaffBillings.FindAsync(id);
            if (bill == null || user == null) return NotFound();

            if (bill.Status == BillingStatus.Pending || bill.Status == BillingStatus.Accepted)
            {
                TempData["Info"] = "Bill này đã được thanh toán hoặc đang chờ Admin duyệt.";
                return RedirectToAction(nameof(Index));
            }

            var paymentUrl = _billingService.CreateBillingPaymentUrl(user, bill, HttpContext);
            return Redirect(paymentUrl);
        }

        // GET: StaffBilling/PaymentCallback
        public async Task<IActionResult> PaymentCallback()
        {
            var user = await _userManager.GetUserAsync(User);
            var response = await _billingService.ExecuteVnPayCallback(Request.Query);

            if (response.Success && response.VnPayResponseCode == "00")
            {
                var bill = await _context.StaffBillings
                    .Where(b => b.UserId == user.Id && b.Month.Year == DateTime.Now.Year && b.Month.Month == DateTime.Now.Month)
                    .FirstOrDefaultAsync();

                if (bill != null)
                {
                    bill.Status = BillingStatus.Pending; // Thanh toán xong → Pending chờ Admin duyệt
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Thanh toán phí hàng tháng thành công! Đang chờ Admin duyệt.";

                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    foreach (var admin in admins)
                    {
                        string message = $"Staff {user.FullName} vừa thanh toán hóa đơn tháng {bill.Month:MM/yyyy}. Đang chờ Admin duyệt.";
                        await SaveNotification(admin.Id, message);
                    }
                }
            }
            else
            {
                TempData["Error"] = "Thanh toán thất bại hoặc đã bị hủy. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: StaffBilling/GenerateBill
        public async Task<IActionResult> GenerateBill()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var now = DateTime.Now;
            int year = now.Year;
            int month = now.Month;

            var existing = await _context.StaffBillings
                .FirstOrDefaultAsync(b => b.UserId == user.Id && b.Month.Year == year && b.Month.Month == month);

            if (existing != null)
            {
                TempData["Info"] = "Bill tháng này đã được tạo rồi.";
                return RedirectToAction(nameof(Index));
            }

            var bill = await _billingService.CalculateMonthlyFee(user.Id, year, month);

            if (bill != null)
            {
                bill.Status = BillingStatus.Unpaid; // Mới tạo → chưa thanh toán
                _context.StaffBillings.Add(bill);
                await _context.SaveChangesAsync();

                string notiMessage = $"Hóa đơn tháng {month}/{year} đã được tạo. Vui lòng thanh toán trong 5 ngày.";
                await SaveNotification(user.Id, notiMessage);

                TempData["Success"] = $"Bill tháng {month}/{year} đã được tạo thành công! Tổng phí: {bill.TotalFee:N0} đ";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
