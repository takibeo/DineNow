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

        // Quản trị viên xem danh sách nhà hàng
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Lấy danh sách nhà hàng do Staff hiện tại quản lý
            var restaurants = await _context.StaffRestaurants
                .Where(sr => sr.UserId == userId)
                .Select(sr => sr.Restaurant)
                .ToListAsync();

            return View(restaurants);
        }


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
                        using (var stream = new FileStream(filePath, FileMode.Create))
                            await file.CopyToAsync(stream);
                        uploadedUrls.Add("/images/restaurants/" + fileName);
                    }
                }
            }

            restaurant.ImageUrl = string.Join(";", uploadedUrls);
            restaurant.IsApproved = false; // Staff thêm → chờ admin phê duyệt
            _context.Restaurants.Add(restaurant);
            await _context.SaveChangesAsync();

            // Gán Staff quản lý nhà hàng
            var staffRestaurant = new StaffRestaurant
            {
                RestaurantId = restaurant.Id,
                UserId = _userManager.GetUserId(User)
            };
            _context.StaffRestaurants.Add(staffRestaurant);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã gửi yêu cầu thêm nhà hàng, chờ admin phê duyệt.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);

            var isOwner = await _context.StaffRestaurants
                .AnyAsync(sr => sr.RestaurantId == id && sr.UserId == userId);

            if (!isOwner) return Forbid(); // Staff không quản lý -> cấm truy cập

            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null) return NotFound();

            return View(restaurant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Restaurant restaurant, IFormFile[] ImageFiles)
        {
            var userId = _userManager.GetUserId(User);

            var isOwner = await _context.StaffRestaurants
                .AnyAsync(sr => sr.RestaurantId == id && sr.UserId == userId);
            if (!isOwner) return Forbid();

            var existing = await _context.Restaurants.FindAsync(id);
            if (existing == null) return NotFound();

            if (ModelState.IsValid)
            {
                // Upload ảnh mới nếu có
                if (ImageFiles != null && ImageFiles.Length > 0)
                {
                    var folder = Path.Combine(_env.WebRootPath, "images", "restaurants");
                    Directory.CreateDirectory(folder);
                    var imageUrls = new List<string>();
                    foreach (var file in ImageFiles)
                    {
                        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                        var filePath = Path.Combine(folder, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                        imageUrls.Add("/images/restaurants/" + fileName);
                    }
                    existing.ImageUrl = string.Join(";", imageUrls);
                }

                // Cập nhật các trường
                existing.Name = restaurant.Name;
                existing.Description = restaurant.Description;
                existing.City = restaurant.City;
                existing.CuisineType = restaurant.CuisineType;
                existing.Address = restaurant.Address;
                existing.AveragePrice = restaurant.AveragePrice;
                existing.IsApproved = false; // chỉnh sửa → chờ phê duyệt lại

                _context.Update(existing);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(restaurant);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);

            var isOwner = await _context.StaffRestaurants
                .AnyAsync(sr => sr.RestaurantId == id && sr.UserId == userId);
            if (!isOwner) return Forbid();

            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null) return NotFound();

            return View(restaurant);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);

            var isOwner = await _context.StaffRestaurants
                .AnyAsync(sr => sr.RestaurantId == id && sr.UserId == userId);
            if (!isOwner) return Forbid();

            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant != null)
            {
                _context.Restaurants.Remove(restaurant);

                // Xóa cả liên kết StaffRestaurant
                var staffLink = await _context.StaffRestaurants
                    .FirstOrDefaultAsync(sr => sr.RestaurantId == id && sr.UserId == userId);
                if (staffLink != null) _context.StaffRestaurants.Remove(staffLink);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }

}
