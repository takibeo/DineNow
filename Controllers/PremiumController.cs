using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using DoAnChuyenNganh.Models.VnPay;
using DoAnChuyenNganh.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DoAnChuyenNganh.Controllers
{
    public class PremiumController : Controller
    {
        private readonly PremiumService _premiumService;
        private readonly UserManager<User> _userManager;
        private readonly AppDBContext _context;

        public PremiumController(PremiumService premiumService, UserManager<User> userManager, AppDBContext context)
        {
            _premiumService = premiumService;
            _userManager = userManager;
            _context = context;
        }

        // GET: Premium/BuyPremium
        [HttpGet]
        public async Task<IActionResult> BuyPremium()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var package = new PremiumPackageModel(); // gói mặc định
            return View(package);
        }

        // POST: Premium/BuyPremium
        [HttpPost]
        public async Task<IActionResult> BuyPremium(PremiumPackageModel package)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var paymentUrl = _premiumService.CreatePremiumPaymentUrl(user, package, HttpContext);

            return Redirect(paymentUrl);
        }

        // GET: Premium/PaymentCallback
        // GET: Premium/PaymentCallback
        [HttpGet]
        public async Task<IActionResult> PaymentCallback()
        {
            var response = await _premiumService.ExecuteVnPayCallback(Request.Query);

            var user = await _userManager.GetUserAsync(User);

            if (response.Success && response.VnPayResponseCode == "00" && user != null)
            {
                // 1. Ghi log
                var log = new UserLog
                {
                    UserId = user.Id,
                    Action = "Upgrade to Premium",
                    Description = $"Bạn đã nâng cấp lên Premium.",
                    Timestamp = DateTime.Now
                };
                _context.UserLogs.Add(log);
                await _context.SaveChangesAsync();

                // 2. Thông báo thành công
                TempData["Success"] = "Bạn đã nâng cấp lên Premium thành công!";
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ViewData["ErrorMessage"] = "Thanh toán không thành công hoặc đã bị hủy. Vui lòng thử lại.";
                return View("PaymentFailed");
            }
        }

    }
}
