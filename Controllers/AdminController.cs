using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using DoAnChuyenNganh.Models.ViewModels;
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

        public AdminController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, AppDBContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
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
            var userRoles = new List<(User user, string role)>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles.Add((user, roles.FirstOrDefault() ?? "None"));
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
                        .Select(sr => sr.User.FullName)
                        .ToList()
                })
                .ToListAsync();

            ViewBag.Restaurants = data;
            return View();
        }

        // ❌ Xóa nhà hàng
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

            // Xóa liên kết StaffRestaurant
            var staffLinks = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == id)
                .ToListAsync();
            _context.StaffRestaurants.RemoveRange(staffLinks);

            // Xóa nhà hàng
            _context.Restaurants.Remove(restaurant);
            await _context.SaveChangesAsync();

            // Gửi thông báo cho Staff quản lý
            foreach (var staffId in staffList)
            {
                await SaveNotification(
                    staffId,
                    $"Admin đã xóa nhà hàng bạn quản lý: {restaurant.Name}."
                );
            }

            TempData["Success"] = $"Đã xóa nhà hàng: {restaurant.Name}";
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
    }
}
