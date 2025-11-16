using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class MenuItemController : Controller
    {
        private readonly AppDBContext _context;
        private readonly IWebHostEnvironment _env;

        public MenuItemController(AppDBContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet]
        public IActionResult Create(int restaurantId)
        {
            var restaurant = _context.Restaurants.FirstOrDefault(r => r.Id == restaurantId);
            if (restaurant == null) return NotFound();
            ViewBag.Restaurant = restaurant;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMultiple(
        int RestaurantId,
        List<string> Names,
        List<decimal> Prices,
        List<string> Descriptions,
        List<string> Categories,   
        List<IFormFile> Images)
        {
            for (int i = 0; i < Names.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(Names[i])) continue;

                string? imagePath = null;
                if (Images.Count > i && Images[i]?.Length > 0)
                {
                    var folder = Path.Combine(_env.WebRootPath, "images", "menuitems");
                    Directory.CreateDirectory(folder);
                    var fileName = Guid.NewGuid() + Path.GetExtension(Images[i].FileName);
                    var filePath = Path.Combine(folder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await Images[i].CopyToAsync(stream);
                    }
                    imagePath = "/images/menuitems/" + fileName;
                }

                _context.MenuItems.Add(new MenuItem
                {
                    RestaurantId = RestaurantId,
                    Name = Names[i],
                    Description = Descriptions[i],
                    Price = Prices[i],
                    Category = Categories[i],
                    ImageUrl = imagePath
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Detail", "Home", new { id = RestaurantId });
        }

        [HttpGet]
        public IActionResult Edit(int restaurantId)
        {
            var restaurant = _context.Restaurants.FirstOrDefault(r => r.Id == restaurantId);
            if (restaurant == null) return NotFound();

            var menuItems = _context.MenuItems
                .Where(m => m.RestaurantId == restaurantId)
                .ToList();

            ViewBag.Restaurant = restaurant;
            return View(menuItems);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int RestaurantId,
            List<int> ItemIds,
            List<string> Names,
            List<decimal> Prices,
            List<string> Descriptions,
            List<IFormFile> Images)
        {
            for (int i = 0; i < ItemIds.Count; i++)
            {
                var item = await _context.MenuItems.FindAsync(ItemIds[i]);
                if (item == null) continue;

                item.Name = Names[i];
                item.Price = Prices[i];
                item.Description = Descriptions[i];

                if (Images.Count > i && Images[i]?.Length > 0)
                {
                    var folder = Path.Combine(_env.WebRootPath, "images", "menuitems");
                    Directory.CreateDirectory(folder);
                    var fileName = Guid.NewGuid() + Path.GetExtension(Images[i].FileName);
                    var filePath = Path.Combine(folder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await Images[i].CopyToAsync(stream);
                    }
                    item.ImageUrl = "/images/menuitems/" + fileName;
                }

                _context.MenuItems.Update(item);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Detail", "Home", new { id = RestaurantId });
        }

        // ✅ Sửa lại hàm Delete để hoạt động với fetch POST
        [HttpPost]
        [IgnoreAntiforgeryToken] // ❗ Bỏ qua kiểm tra token (vì fetch không gửi form thực tế)
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null)
                return NotFound();

            int restaurantId = item.RestaurantId;
            _context.MenuItems.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, restaurantId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadMenuImage(int RestaurantId, List<IFormFile> MenuImages)
        {
            if (MenuImages == null || !MenuImages.Any())
                return BadRequest("Không có hình ảnh nào được chọn.");

            var folder = Path.Combine(_env.WebRootPath, "images", "menuuploads");
            Directory.CreateDirectory(folder);

            foreach (var image in MenuImages)
            {
                if (image != null && image.Length > 0)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(image.FileName);
                    var filePath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    // 👉 Lưu mỗi ảnh như 1 "MenuItem" kiểu "MenuImage"
                    _context.MenuItems.Add(new MenuItem
                    {
                        RestaurantId = RestaurantId,
                        Name = "Ảnh thực đơn",
                        Description = "Menu hình ảnh tải lên",
                        Price = 0,
                        Category = "Menu ảnh",
                        ImageUrl = "/images/menuuploads/" + fileName
                    });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Detail", "Home", new { id = RestaurantId });
        }

    }
}
