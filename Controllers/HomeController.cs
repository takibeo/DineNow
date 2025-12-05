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
        public async Task<IActionResult> Index(
    string? search,
    string? city,
    string? address,
    string? cuisine,
    int? rating,
    decimal? minPrice,
    decimal? maxPrice)
        {
            var query = _context.Restaurants
                .Include(r => r.Reviews)
                .Where(r => r.IsApproved == true)
                .AsQueryable();

            // 🔎 Tìm kiếm theo tên hoặc mô tả
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r =>
                    r.Name.Contains(search) ||
                    r.Description.Contains(search)
                );
            }

            // 🏙 Thành phố
            if (!string.IsNullOrEmpty(city))
                query = query.Where(r => r.City.Contains(city));

            // 📍 Địa chỉ
            if (!string.IsNullOrEmpty(address))
                query = query.Where(r => r.Address.Contains(address));

            // 🍱 Loại món
            if (!string.IsNullOrEmpty(cuisine))
                query = query.Where(r => r.CuisineType.Contains(cuisine));

            // ⭐ Rating
            if (rating.HasValue)
            {
                query = query.Where(r =>
                    r.Reviews.Any() &&
                    r.Reviews.Average(rv => rv.Rating) >= rating.Value
                );
            }

            // 💰 Giá trung bình
            if (minPrice.HasValue)
                query = query.Where(r => r.AveragePrice >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(r => r.AveragePrice <= maxPrice.Value);

            // Lấy data + rating trung bình
            var restaurants = await query
                .Select(r => new
                {
                    Restaurant = r,
                    AverageRating = r.Reviews.Any() ? r.Reviews.Average(rv => rv.Rating) : 0
                })
                .ToListAsync();

            // Truyền rating sang view
            ViewBag.RestaurantRatings = restaurants.ToDictionary(
                x => x.Restaurant.Id, x => x.AverageRating);

            // Dropdown dữ liệu
            ViewBag.Cities = await _context.Restaurants.Select(r => r.City).Distinct().ToListAsync();
            ViewBag.Cuisines = await _context.Restaurants.Select(r => r.CuisineType).Distinct().ToListAsync();

            var user = await _userManager.GetUserAsync(User);
            var favoriteIds = new List<int>();

            if (user != null)
            {
                favoriteIds = await _context.FavoriteRestaurants
                    .Where(f => f.UserId == user.Id)
                    .Select(f => f.RestaurantId)
                    .ToListAsync();
            }

            ViewBag.FavoriteIds = favoriteIds;
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

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ToggleFavorite(int restaurantId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            var existing = await _context.FavoriteRestaurants
                .FirstOrDefaultAsync(f => f.UserId == user.Id && f.RestaurantId == restaurantId);

            if (existing == null)
            {
                // ➕ Thêm yêu thích
                _context.FavoriteRestaurants.Add(new FavoriteRestaurant
                {
                    UserId = user.Id,
                    RestaurantId = restaurantId
                });

                await _context.SaveChangesAsync();
                return Json(new { isFavorite = true });
            }
            else
            {
                // ❌ Xóa yêu thích
                _context.FavoriteRestaurants.Remove(existing);
                await _context.SaveChangesAsync();
                return Json(new { isFavorite = false });
            }
        }

        [Authorize]
        public async Task<IActionResult> Favorite()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized();

            var favorites = await _context.FavoriteRestaurants
                .Where(f => f.UserId == user.Id)
                .Include(f => f.Restaurant)
                    .ThenInclude(r => r.Reviews)
                .Select(f => f.Restaurant)
                .ToListAsync();

            // Tính rating
            ViewBag.RestaurantRatings = favorites.ToDictionary(
                x => x.Id,
                x => x.Reviews.Any() ? x.Reviews.Average(r => r.Rating) : 0
            );

            return View(favorites);
        }

    }
}
