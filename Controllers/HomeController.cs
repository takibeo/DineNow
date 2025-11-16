using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDBContext _context;
        private readonly UserManager<User> _userManager;

        public HomeController(AppDBContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Trang chủ hiển thị danh sách nhà hàng
        public async Task<IActionResult> Index()
        {
            // Chỉ lấy nhà hàng đã được duyệt
            var restaurants = await _context.Restaurants
                .Include(r => r.Reviews)
                .Where(r => r.IsApproved == true)
                .Select(r => new
                {
                    Restaurant = r,
                    AverageRating = r.Reviews.Any() ? r.Reviews.Average(rv => rv.Rating) : 0
                })
                .ToListAsync();

            // Truyền trung bình đánh giá sang ViewBag
            ViewBag.RestaurantRatings = restaurants.ToDictionary(x => x.Restaurant.Id, x => x.AverageRating);

            // Trả về danh sách nhà hàng
            return View(restaurants.Select(x => x.Restaurant).ToList());
        }



        // Trang chi tiết (cho người dùng xem, không cần đăng nhập)
        public async Task<IActionResult> Detail(int id)
        {
            var restaurant = await _context.Restaurants
                .Include(r => r.MenuItems)
                .Include(r => r.Reviews!)
                    .ThenInclude(rv => rv.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (restaurant == null)
                return NotFound();

            return View(restaurant);
        }

        // ✅ Gửi review mới
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddReview(int restaurantId, int rating, string comment)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            var review = new Review
            {
                RestaurantId = restaurantId,
                UserId = user.Id,
                Rating = rating,
                Comment = comment,
                CreatedAt = DateTime.Now
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return RedirectToAction("Detail", new { id = restaurantId });
        }
    }
}
