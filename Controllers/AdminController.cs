using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using DoAnChuyenNganh.Models.ViewModels;
using DoAnChuyenNganh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDBContext _context;
        private readonly BillingService _billingService;

        public AdminController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, AppDBContext context, BillingService billingService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _billingService = billingService;
        }

        private async Task SaveNotification(string userId, string message)
        {
            // Lấy thông tin người gửi (Admin hoặc Staff)
            var sender = await _userManager.GetUserAsync(User);
            var phone = sender?.PhoneNumber ?? "Không có";

            // Thêm dòng hỗ trợ vào message
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
        // 📋 Danh sách tài khoản
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();

            // Chỉ cần User + Role (2 phần tử) để khớp với View
            var userRoles = new List<(User user, string role)>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                string role = roles.FirstOrDefault() ?? "None";

                userRoles.Add((user, role));
            }

            return View(userRoles);
        }

        // ✏️ Sửa tài khoản
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            var userRoles = await _userManager.GetRolesAsync(user);

            ViewBag.Roles = roles;
            ViewBag.UserRole = userRoles.FirstOrDefault();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, User model, string role)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var oldRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, oldRoles);
                await _userManager.AddToRoleAsync(user, role);
                TempData["Success"] = "Cập nhật tài khoản thành công!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Cập nhật thất bại!";
            return View(model);
        }

        // 🔒 Khóa hoặc mở khóa tài khoản
        [HttpPost]
        public async Task<IActionResult> ToggleLock(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var admin = await _userManager.GetUserAsync(User); // Admin hiện tại
            if (admin == null) return Forbid();

            // Nếu user chưa bị khóa hoặc đã hết hạn khóa
            if (user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.Now)
            {
                user.LockoutEnabled = true;                 // bật chế độ khóa
                user.LockoutEnd = DateTimeOffset.Now.AddYears(100); // khóa dài hạn
                user.LockedByAdminId = admin.Id;            // lưu admin đã khóa

                TempData["Success"] = $"Đã khóa tài khoản {user.UserName}";
            }
            else // Mở khóa
            {
                user.LockoutEnd = null;
                user.LockedByAdminId = null; // xóa admin đã khóa
                TempData["Success"] = $"Đã mở khóa tài khoản {user.UserName}";
            }

            await _userManager.UpdateAsync(user);
            return RedirectToAction(nameof(Index));
        }

        // ❌ Xóa tài khoản
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // 🔹 Xóa tất cả dữ liệu liên quan
            var reservations = _context.Reservations.Where(r => r.UserId == id);
            var reviews = _context.Reviews.Where(r => r.UserId == id);
            var notifications = _context.Notifications.Where(n => n.UserId == id);
            var logs = _context.UserLogs.Where(l => l.UserId == id);
            var aiRecs = _context.AIRecommendations.Where(a => a.UserId == id);

            _context.Reservations.RemoveRange(reservations);
            _context.Reviews.RemoveRange(reviews);
            _context.Notifications.RemoveRange(notifications);
            _context.UserLogs.RemoveRange(logs);
            _context.AIRecommendations.RemoveRange(aiRecs);

            await _context.SaveChangesAsync(); // Lưu thay đổi trước khi xóa user

            // 🔹 Xóa user
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Xóa tài khoản thất bại!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Đã xóa tài khoản {user.UserName} và tất cả dữ liệu liên quan!";
            return RedirectToAction(nameof(Index));
        }


        // 🏢 Xem toàn bộ nhà hàng + Staff quản lý
        public async Task<IActionResult> ManageRestaurants()
        {
            var data = await _context.Restaurants
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.IsApproved,
                    Staffs = _context.StaffRestaurants
                                .Where(sr => sr.RestaurantId == r.Id)
                                .Select(sr => new
                                {
                                    sr.User.Id,
                                    sr.User.FullName,
                                    LastBillMonth = _context.StaffBillings
                                        .Where(b => b.UserId == sr.User.Id)
                                        .OrderByDescending(b => b.Month)
                                        .Select(b => b.Month)
                                        .FirstOrDefault()
                                })
                                .ToList()
                })
                .ToListAsync();

            ViewBag.Restaurants = data;
            return View();
        }

        // ❌ Xóa nhà hàng
        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> DeleteRestaurant(int id)
        {
            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null)
            {
                TempData["Error"] = "Không tìm thấy nhà hàng!";
                return RedirectToAction(nameof(ManageRestaurants));
            }

            // Lấy danh sách Staff quản lý trước khi xóa
            var staffList = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == restaurant.Id)
                .Select(sr => sr.UserId)
                .ToListAsync();

            // 🔹 Xóa liên kết StaffRestaurant
            var staffLinks = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == id)
                .ToListAsync();
            _context.StaffRestaurants.RemoveRange(staffLinks);

            // 🔹 Xóa menu của nhà hàng
            var menus = await _context.MenuItems
                .Where(m => m.RestaurantId == id)
                .ToListAsync();
            _context.MenuItems.RemoveRange(menus);

            // 🔹 Xóa các review
            var reviews = await _context.Reviews
                .Where(r => r.RestaurantId == id)
                .ToListAsync();
            _context.Reviews.RemoveRange(reviews);

            // 🔹 Xóa các reservation
            var reservations = await _context.Reservations
                .Where(r => r.RestaurantId == id)
                .ToListAsync();
            _context.Reservations.RemoveRange(reservations);

            // 🔹 Xóa các đơn hàng (Orders)
            var orders = await _context.Orders
                .Where(o => o.RestaurantId == id)
                .ToListAsync();
            _context.Orders.RemoveRange(orders);

            // 🔹 Xóa thông báo liên quan đến staff quản lý
            var notifications = await _context.Notifications
                .Where(n => staffList.Contains(n.UserId))
                .ToListAsync();
            _context.Notifications.RemoveRange(notifications);

            // 🔹 Lưu các thay đổi trước khi xóa nhà hàng
            await _context.SaveChangesAsync();

            // 🔹 Xóa nhà hàng
            _context.Restaurants.Remove(restaurant);
            await _context.SaveChangesAsync();

            // 🔹 Gửi thông báo cho Staff quản lý
            foreach (var staffId in staffList)
            {
                await SaveNotification(
                    staffId,
                    $"Admin đã xóa nhà hàng bạn quản lý: {restaurant.Name}."
                );
            }

            TempData["Success"] = $"Đã xóa nhà hàng: {restaurant.Name} ";
            return RedirectToAction(nameof(ManageRestaurants));
        }


        // ✅ Phê duyệt nhà hàng
        [HttpPost]
        public async Task<IActionResult> ApproveRestaurant(int id)
        {
            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null) return NotFound();

            restaurant.IsApproved = true;
            await _context.SaveChangesAsync();

            // Thông báo cho Staff quản lý
            var staffList = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == restaurant.Id)
                .Select(sr => sr.UserId)
                .ToListAsync();

            foreach (var staffId in staffList)
            {
                await SaveNotification(
                    staffId,
                    $"Nhà hàng '{restaurant.Name}' đã được Admin phê duyệt."
                );
            }

            TempData["Success"] = $"Đã phê duyệt nhà hàng: {restaurant.Name}";
            return RedirectToAction(nameof(ManageRestaurants));
        }

        // ✅ Tạo hóa đơn hàng tháng cho 1 Staff (tự động tính tổng tất cả nhà hàng họ quản lý)
        private async Task<StaffBilling> GenerateMonthlyBill(string staffId, int? year = null, int? month = null)
        {
            if (string.IsNullOrEmpty(staffId)) return null;

            var staff = await _userManager.FindByIdAsync(staffId);
            if (staff == null) return null;

            int y = year ?? DateTime.Now.Year;
            int m = month ?? DateTime.Now.Month;

            // Kiểm tra bill đã tồn tại
            var existingBill = await _context.StaffBillings
                .FirstOrDefaultAsync(b => b.UserId == staffId && b.Month.Year == y && b.Month.Month == m);

            if (existingBill != null)
                return existingBill; // Bill đã có → trả về luôn

            var bill = await _billingService.CalculateMonthlyFee(staffId, y, m);

            if (bill == null || bill.TotalFee <= 0)
                return null;

            string notiMessage = $"Hóa đơn thanh toán tháng {m}/{y} đã được tạo. Tổng phí: {bill.TotalFee:N0} đ. Vui lòng thanh toán trong 5 ngày.";
            await SaveNotification(staffId, notiMessage);

            return bill;
        }
        // ✅ Nút tạo bill cho Staff quản lý (gọi GenerateMonthlyBill)
        [HttpPost]
        public async Task<IActionResult> CreateBillForStaff(string staffId)
        {
            if (string.IsNullOrEmpty(staffId))
                return BadRequest(new { success = false, message = "Staff không hợp lệ." });

            var staff = await _userManager.FindByIdAsync(staffId);
            if (staff == null)
                return NotFound(new { success = false, message = "Staff không tồn tại." });

            var bill = await GenerateMonthlyBill(staffId);

            if (bill == null)
                return BadRequest(new { success = false, message = "Bill chưa được tạo (có thể đã tồn tại hoặc Staff chưa quản lý nhà hàng nào)." });

            return Ok(new
            {
                success = true,
                message = $"Hóa đơn đã được gửi đến Staff {staff.FullName}.",
                billId = bill.Id
            });
        }

        // GET: Admin/ManagePayment
        public async Task<IActionResult> ManagePayment()
        {
            var staffUsers = await _userManager.GetUsersInRoleAsync("Staff");

            var staffBillings = new List<ManagePaymentViewModel>();
            var now = DateTime.Now;

            foreach (var staff in staffUsers)
            {
                var lastBill = await _context.StaffBillings
                    .Where(b => b.UserId == staff.Id)
                    .OrderByDescending(b => b.Month)
                    .FirstOrDefaultAsync();

                var hasRestaurants = await _context.StaffRestaurants
                    .AnyAsync(sr => sr.UserId == staff.Id);

                staffBillings.Add(new ManagePaymentViewModel
                {
                    StaffId = staff.Id,
                    StaffName = staff.FullName,
                    LastBillMonth = lastBill?.Month,
                    Status = lastBill?.Status ?? BillingStatus.Unpaid,
                    HasManagedRestaurants = hasRestaurants
                });
            }

            return View(staffBillings);
        }

        [HttpPost]
        public async Task<IActionResult> AcceptPayment(string staffId)
        {
            if (string.IsNullOrEmpty(staffId))
                return Json(new { success = false, message = "Staff không hợp lệ." });

            var lastBill = await _context.StaffBillings
                .Where(b => b.UserId == staffId)
                .OrderByDescending(b => b.Month)
                .FirstOrDefaultAsync();

            if (lastBill == null)
                return Json(new { success = false, message = "Không tìm thấy hóa đơn cho Staff này." });

            lastBill.Status = BillingStatus.Accepted;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Thanh toán đã được Admin chấp nhận." });
        }

        [HttpPost]
        public async Task<IActionResult> RejectPayment(string staffId)
        {
            if (string.IsNullOrEmpty(staffId))
                return Json(new { success = false, message = "Staff không hợp lệ." });

            var lastBill = await _context.StaffBillings
                .Where(b => b.UserId == staffId)
                .OrderByDescending(b => b.Month)
                .FirstOrDefaultAsync();

            if (lastBill == null)
                return Json(new { success = false, message = "Không tìm thấy hóa đơn cho Staff này." });

            lastBill.Status = BillingStatus.Rejected;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Thanh toán đã bị Admin từ chối." });
        }

    }
}
