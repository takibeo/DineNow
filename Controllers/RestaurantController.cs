using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class RestaurantController : Controller
    {
        private readonly AppDBContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<User> _userManager;

        public RestaurantController(AppDBContext context, IWebHostEnvironment env, UserManager<User> userManager)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        private async Task SaveNotification(string userId, string message)
        {
            var sender = await _userManager.GetUserAsync(User);
            var staffName = !string.IsNullOrEmpty(sender?.FullName) ? sender.FullName : sender?.UserName;
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

        // Index: Danh sách nhà hàng
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var restaurants = await _context.StaffRestaurants
                .Where(sr => sr.UserId == userId)
                .Select(sr => sr.Restaurant)
                .ToListAsync();

            return View(restaurants);
        }

        // Create
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Restaurant restaurant, List<IFormFile>? ImageFiles)
        {
            if (!ModelState.IsValid) return View(restaurant);

            // Upload ảnh
            var folder = Path.Combine(_env.WebRootPath, "images", "restaurants");
            Directory.CreateDirectory(folder);
            var uploadedUrls = new List<string>();

            if (ImageFiles != null && ImageFiles.Any())
            {
                foreach (var file in ImageFiles)
                {
                    if (file.Length > 0)
                    {
                        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                        var filePath = Path.Combine(folder, fileName);
                        using var stream = new FileStream(filePath, FileMode.Create);
                        await file.CopyToAsync(stream);

                        uploadedUrls.Add("/images/restaurants/" + fileName);
                    }
                }
            }

            restaurant.ImageUrl = string.Join(";", uploadedUrls);
            restaurant.IsApproved = false;

            _context.Restaurants.Add(restaurant);
            await _context.SaveChangesAsync();

            // Gán Staff quản lý
            var staffRestaurant = new StaffRestaurant
            {
                RestaurantId = restaurant.Id,
                UserId = _userManager.GetUserId(User)
            };
            _context.StaffRestaurants.Add(staffRestaurant);
            await _context.SaveChangesAsync();

            // Thông báo Admin
            var adminList = await _userManager.GetUsersInRoleAsync("Admin");
            var staff = await _userManager.GetUserAsync(User);
            var staffName = !string.IsNullOrEmpty(staff?.FullName) ? staff.FullName : staff?.UserName;

            foreach (var admin in adminList)
            {
                await SaveNotification(
                    admin.Id,
                    $"Staff '{staffName}' đã tạo yêu cầu thêm nhà hàng: {restaurant.Name}."
                );
            }

            TempData["Success"] = "Đã gửi yêu cầu thêm nhà hàng, chờ admin phê duyệt.";
            return RedirectToAction(nameof(Index));
        }

        // Edit GET
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var isOwner = await _context.StaffRestaurants
                    .AnyAsync(sr => sr.RestaurantId == id && sr.UserId == userId);
                if (!isOwner) return Forbid();
            }

            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null) return NotFound();

            return View(restaurant);
        }

        // Edit POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Restaurant restaurant, IFormFile[] ImageFiles)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var isOwner = await _context.StaffRestaurants
                    .AnyAsync(sr => sr.RestaurantId == id && sr.UserId == userId);
                if (!isOwner) return Forbid();
            }

            var existing = await _context.Restaurants.FindAsync(id);
            if (existing == null) return NotFound();

            if (ModelState.IsValid)
            {
                if (ImageFiles != null && ImageFiles.Length > 0)
                {
                    var folder = Path.Combine(_env.WebRootPath, "images", "restaurants");
                    Directory.CreateDirectory(folder);
                    var imageUrls = new List<string>();
                    foreach (var file in ImageFiles)
                    {
                        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                        var filePath = Path.Combine(folder, fileName);
                        using var stream = new FileStream(filePath, FileMode.Create);
                        await file.CopyToAsync(stream);
                        imageUrls.Add("/images/restaurants/" + fileName);
                    }
                    existing.ImageUrl = string.Join(";", imageUrls);
                }

                existing.Name = restaurant.Name;
                existing.Description = restaurant.Description;
                existing.City = restaurant.City;
                existing.CuisineType = restaurant.CuisineType;
                existing.Address = restaurant.Address;
                existing.AveragePrice = restaurant.AveragePrice;

                if (!isAdmin) existing.IsApproved = false;

                _context.Update(existing);
                await _context.SaveChangesAsync();

                var staff = await _userManager.GetUserAsync(User);
                var staffName = !string.IsNullOrEmpty(staff?.FullName) ? staff.FullName : staff?.UserName;

                if (!isAdmin)
                {
                    var adminList = await _userManager.GetUsersInRoleAsync("Admin");
                    foreach (var admin in adminList)
                    {
                        await SaveNotification(
                            admin.Id,
                            $"Staff '{staffName}' đã chỉnh sửa thông tin nhà hàng: {existing.Name}. Nhà hàng cần phê duyệt lại."
                        );
                    }
                }
                else
                {
                    var staffList = await _context.StaffRestaurants
                        .Where(sr => sr.RestaurantId == existing.Id)
                        .Select(sr => sr.UserId)
                        .ToListAsync();
                    foreach (var staffId in staffList)
                    {
                        await SaveNotification(
                            staffId,
                            $"Admin đã chỉnh sửa thông tin nhà hàng bạn đang quản lý: {existing.Name}."
                        );
                    }
                }

                return isAdmin ? RedirectToAction("ManageRestaurants", "Admin") : RedirectToAction(nameof(Index));
            }

            return View(restaurant);
        }

        // Delete GET
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var isOwner = await _context.StaffRestaurants
                    .AnyAsync(sr => sr.RestaurantId == id && sr.UserId == userId);
                if (!isOwner) return Forbid();
            }

            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null) return NotFound();

            return View(restaurant);
        }

        // Delete POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin)
            {
                var isOwner = await _context.StaffRestaurants
                    .AnyAsync(sr => sr.RestaurantId == id && sr.UserId == userId);
                if (!isOwner) return Forbid();
            }

            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null) return NotFound();

            var staffList = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == restaurant.Id)
                .Select(sr => sr.UserId)
                .ToListAsync();

            _context.Restaurants.Remove(restaurant);

            if (!isAdmin)
            {
                var staffLink = await _context.StaffRestaurants
                    .FirstOrDefaultAsync(sr => sr.RestaurantId == id && sr.UserId == userId);
                if (staffLink != null) _context.StaffRestaurants.Remove(staffLink);
            }
            else
            {
                var allLinks = await _context.StaffRestaurants
                    .Where(sr => sr.RestaurantId == id)
                    .ToListAsync();
                _context.StaffRestaurants.RemoveRange(allLinks);
            }

            await _context.SaveChangesAsync();

            var staff = await _userManager.GetUserAsync(User);
            var staffName = !string.IsNullOrEmpty(staff?.FullName) ? staff.FullName : staff?.UserName;

            if (!isAdmin)
            {
                var adminList = await _userManager.GetUsersInRoleAsync("Admin");
                foreach (var admin in adminList)
                {
                    await SaveNotification(
                        admin.Id,
                        $"Staff '{staffName}' đã xóa nhà hàng: {restaurant.Name}"
                    );
                }
            }
            else
            {
                foreach (var staffId in staffList)
                {
                    await SaveNotification(
                        staffId,
                        $"Admin đã xóa nhà hàng bạn đang quản lý: {restaurant.Name}"
                    );
                }
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
