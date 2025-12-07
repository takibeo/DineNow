using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using DoAnChuyenNganh.Services;
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
        private readonly SentimentService _sentiment;
        private readonly AIRecommendationService _aiRecommend;
        private readonly CollaborativeFilteringService _cf;

        public HomeController(
            AppDBContext context,
            UserManager<User> userManager,
            SentimentService sentiment,
            AIRecommendationService aiRecommend,
            CollaborativeFilteringService cf
        )
        {
            _context = context;
            _userManager = userManager;
            _sentiment = sentiment;
            _aiRecommend = aiRecommend;
            _cf = cf;
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
                .Where(r => r.IsApproved)
                .AsQueryable();

            // 🔎 Search
            if (!string.IsNullOrEmpty(search))
                query = query.Where(r =>
                    r.Name.Contains(search) ||
                    r.Description.Contains(search)
                );

            // 🏙 City
            if (!string.IsNullOrEmpty(city))
                query = query.Where(r => r.City.Contains(city));

            // 📍 Address
            if (!string.IsNullOrEmpty(address))
                query = query.Where(r => r.Address.Contains(address));

            // 🍱 Cuisine
            if (!string.IsNullOrEmpty(cuisine))
                query = query.Where(r => r.CuisineType.Contains(cuisine));

            // ⭐ Rating
            if (rating.HasValue)
                query = query.Where(r =>
                    r.Reviews.Any() &&
                    r.Reviews.Average(rv => rv.Rating) >= rating.Value
                );

            // 💰 Price
            if (minPrice.HasValue)
                query = query.Where(r => r.AveragePrice >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(r => r.AveragePrice <= maxPrice.Value);

            // ✅ Lấy restaurants + rating
            var restaurants = await query
                .Select(r => new
                {
                    Restaurant = r,
                    AverageRating = r.Reviews.Any() ?
                        r.Reviews.Average(rv => rv.Rating) :
                        0
                })
                .ToListAsync();

            ViewBag.RestaurantRatings = restaurants.ToDictionary(
                x => x.Restaurant.Id,
                x => x.AverageRating
            );

            // Dropdown
            ViewBag.Cities = await _context.Restaurants
                .Select(r => r.City)
                .Distinct()
                .ToListAsync();

            ViewBag.Cuisines = await _context.Restaurants
                .Select(r => r.CuisineType)
                .Distinct()
                .ToListAsync();

            // ❤️ Favorite
            var user = await _userManager.GetUserAsync(User);
            var favoriteIds = new List<int>();

            List<Restaurant> recommendations = new();

            if (user != null)
            {
                favoriteIds = await _context.FavoriteRestaurants
                    .Where(f => f.UserId == user.Id)
                    .Select(f => f.RestaurantId)
                    .ToListAsync();

                // ⭐ Lấy số review user đã đăng
                int reviewCount = await _context.Reviews
                    .Where(r => r.UserId == user.Id)
                    .CountAsync();

                // ✅ Nếu user có review → dùng Collaborative Filtering
                if (reviewCount >= 3)
                {
                    recommendations = await _cf.GetRecommendationsAsync(user.Id, 5);
                }
                else
                {
                    // ✅ Nếu user mới → dùng AI
                    recommendations = await _aiRecommend
                        .GetRecommendedAsync(user.Id, 5);
                }
            }

            ViewBag.Recommendations = recommendations;
            ViewBag.FavoriteIds = favoriteIds;

            return View(restaurants.Select(x => x.Restaurant).ToList());
        }

        // Trang chi tiết (cho người dùng xem, không cần đăng nhập)
        public async Task<IActionResult> Detail(int id)
        {
            var restaurant = await _context.Restaurants
                .Include(r => r.MenuItems)  // <-- BỔ SUNG DÒNG NÀY
                .Include(r => r.Reviews)
                    .ThenInclude(rv => rv.User)
                .Include(r => r.Reviews)
                    .ThenInclude(rv => rv.SentimentAnalysis)
                .Include(r => r.Reviews)
                    .ThenInclude(rv => rv.Replies)
                        .ThenInclude(rep => rep.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (restaurant == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);
            bool canViewAI = false;

            if (user != null)
            {
                bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

                bool isStaffManaging = _context.StaffRestaurants
                    .Any(s => s.RestaurantId == id && s.UserId == user.Id);

                canViewAI = isAdmin || isStaffManaging;
            }

            ViewBag.CanViewAI = canViewAI;

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

            // 1️⃣ Lưu review trước
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

            // 2️⃣ GỌI AI PHÂN TÍCH REVIEW
            var result = await _sentiment.AnalyzeAsync(comment);

            // 3️⃣ Lưu lại SentimentAnalysisLog
            var log = new SentimentAnalysisLog
            {
                ReviewId = review.Id,
                SentimentLabel = result.label,
                SentimentScore = result.score,
                AnalyzedAt = DateTime.Now
            };

            _context.SentimentAnalysisLogs.Add(log);
            await _context.SaveChangesAsync();
            await _aiRecommend.GenerateForUserAsync(user.Id);

            return RedirectToAction("Detail", new { id = restaurantId });
        }

        [Authorize] // chỉ cần đăng nhập
        [HttpPost]
        public async Task<IActionResult> ReplyReview(int reviewId, string replyText)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var review = await _context.Reviews
                .Include(r => r.Restaurant)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null)
                return NotFound();

            var reply = new ReviewReply
            {
                ReviewId = reviewId,
                UserId = user.Id,
                ReplyText = replyText,
                ReplyAt = DateTime.Now
            };

            _context.ReviewReplies.Add(reply);
            await _context.SaveChangesAsync();

            return RedirectToAction("Detail", new { id = review.RestaurantId });
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
