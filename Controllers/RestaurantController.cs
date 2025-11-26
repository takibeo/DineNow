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
            var restaurants = await _context.Restaurants.ToListAsync();
            return View(restaurants);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Restaurant restaurant, List<IFormFile>? ImageFiles)
        {
            if (ModelState.IsValid)
            {
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

                // Lưu tất cả URL vào ImageUrl, phân tách bằng ;
                restaurant.ImageUrl = string.Join(";", uploadedUrls);

                // Staff thêm → cần phê duyệt và tạo StaffRestaurant
                if (User.IsInRole("Staff"))
                {
                    restaurant.IsApproved = false;
                    _context.Restaurants.Add(restaurant);
                    await _context.SaveChangesAsync();

                    var staffRestaurant = new StaffRestaurant
                    {
                        RestaurantId = restaurant.Id,
                        UserId = _userManager.GetUserId(User)
                    };
                    _context.StaffRestaurants.Add(staffRestaurant);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Đã gửi yêu cầu thêm nhà hàng, chờ admin phê duyệt.";
                }
                else
                {
                    restaurant.IsApproved = true;
                    _context.Restaurants.Add(restaurant);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Thêm nhà hàng thành công!";
                }

                return RedirectToAction("Index", "Home");
            }

            return View(restaurant);
        }


        public async Task<IActionResult> Edit(int id)
        {
            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null) return NotFound();
            return View(restaurant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Restaurant restaurant, IFormFile? ImageFile)
        {
            if (id != restaurant.Id) return NotFound();

            var existing = await _context.Restaurants.FindAsync(id);
            if (existing == null) return NotFound();

            if (ModelState.IsValid)
            {
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var folder = Path.Combine(_env.WebRootPath, "images", "restaurants");
                    Directory.CreateDirectory(folder);
                    var fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
                    var filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(stream);
                    }

                    existing.ImageUrl = "/images/restaurants/" + fileName;
                }

                // Cập nhật tất cả các trường từ form
                existing.Name = restaurant.Name;
                existing.Description = restaurant.Description;
                existing.City = restaurant.City;
                existing.CuisineType = restaurant.CuisineType;
                existing.Address = restaurant.Address;
                existing.AveragePrice = restaurant.AveragePrice;

                // Nếu Staff chỉnh sửa => cần duyệt lại
                if (User.IsInRole("Staff"))
                {
                    existing.IsApproved = false;
                    TempData["Success"] = "Đã gửi yêu cầu chỉnh sửa, chờ admin phê duyệt.";
                }
                else
                {
                    existing.IsApproved = true;
                }

                _context.Update(existing);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Home");
            }

            return View(restaurant);
        }



        public async Task<IActionResult> Delete(int id)
        {
            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null) return NotFound();
            return View(restaurant);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant != null)
            {
                _context.Restaurants.Remove(restaurant);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index","Home");
        }
    }

}
