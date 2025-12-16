using DoAnChuyenNganh.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DoAnChuyenNganh.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffReportController : Controller
    {
        private readonly AppDBContext _context;

        public StaffReportController(AppDBContext context)
        {
            _context = context;
        }

        // ================= TRANG CHỌN BÁO CÁO =================
        public IActionResult Index()
        {
            return View();
        }

        // ================= BÁO CÁO ĐẶT BÀN =================
        public async Task<IActionResult> ConfirmedReservations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var restaurantIds = await _context.StaffRestaurants
                .Where(s => s.UserId == userId)
                .Select(s => s.RestaurantId)
                .ToListAsync();

            ViewBag.Restaurants = await _context.Restaurants
                .Where(r => restaurantIds.Contains(r.Id))
                .ToListAsync();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetConfirmedReservationData(
            int? restaurantId,
            string groupBy,
            int? month,
            int? year)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var restaurantIds = await _context.StaffRestaurants
                .Where(s => s.UserId == userId)
                .Select(s => s.RestaurantId)
                .ToListAsync();

            var query = _context.Reservations
                .Where(r =>
                    r.Status == "Confirmed" &&
                    restaurantIds.Contains(r.RestaurantId));

            if (restaurantId.HasValue)
                query = query.Where(r => r.RestaurantId == restaurantId);

            year ??= DateTime.Now.Year;

            // ===== THEO NGÀY =====
            if (groupBy == "Day")
            {
                if (!month.HasValue)
                    return BadRequest("Thiếu tháng");

                var daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);

                var data = await query
                    .Where(r =>
                        r.ReservationDate.Year == year &&
                        r.ReservationDate.Month == month)
                    .GroupBy(r => r.ReservationDate.Day)
                    .Select(g => new
                    {
                        day = g.Key,
                        count = g.Count()
                    })
                    .ToListAsync();

                var result = Enumerable.Range(1, daysInMonth)
                    .Select(d => new
                    {
                        label = $"{d}/{month}/{year}",
                        count = data.FirstOrDefault(x => x.day == d)?.count ?? 0
                    });

                return Json(result);
            }

            // ===== THEO THÁNG =====
            if (groupBy == "Month")
            {
                var data = await query
                    .Where(r => r.ReservationDate.Year == year)
                    .GroupBy(r => r.ReservationDate.Month)
                    .Select(g => new
                    {
                        month = g.Key,
                        count = g.Count()
                    })
                    .ToListAsync();

                var result = Enumerable.Range(1, 12)
                    .Select(m => new
                    {
                        label = $"Tháng {m}/{year}",
                        count = data.FirstOrDefault(x => x.month == m)?.count ?? 0
                    });

                return Json(result);
            }

            // ===== THEO NĂM =====
            var years = await query
                .GroupBy(r => r.ReservationDate.Year)
                .Select(g => new
                {
                    label = g.Key.ToString(),
                    count = g.Count()
                })
                .OrderBy(x => x.label)
                .ToListAsync();

            return Json(years);
        }

        // ================= VIEW DOANH THU ĐỒ ĂN =================
        public async Task<IActionResult> RevenueReport()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var restaurantIds = await _context.StaffRestaurants
                .Where(s => s.UserId == userId)
                .Select(s => s.RestaurantId)
                .ToListAsync();

            ViewBag.Restaurants = await _context.Restaurants
                .Where(r => restaurantIds.Contains(r.Id))
                .ToListAsync();

            return View();
        }

        // ================= API DOANH THU ĐỒ ĂN (STAFF NHẬN) =================
        [HttpGet]
        public async Task<IActionResult> GetFoodOrderRevenue(
            int? restaurantId,
            string groupBy,
            int? year)
        {
            if (groupBy != "Month")
                return BadRequest("Staff chỉ xem doanh thu theo tháng");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var restaurantIds = await _context.StaffRestaurants
                .Where(s => s.UserId == userId)
                .Select(s => s.RestaurantId)
                .ToListAsync();

            var query = _context.Orders
                .Where(o =>
                    o.Status == "Confirmed" &&
                    restaurantIds.Contains(o.RestaurantId));

            if (restaurantId.HasValue)
                query = query.Where(o => o.RestaurantId == restaurantId);

            year ??= DateTime.Now.Year;

            var data = await query
                .Where(o => o.CreatedAt.Year == year)
                .GroupBy(o => o.CreatedAt.Month)
                .Select(g => new
                {
                    month = g.Key,
                    revenue = g.Sum(x => x.TotalAmount - x.AdminCommission)
                })
                .ToListAsync();

            var result = Enumerable.Range(1, 12)
                .Select(m => new
                {
                    label = $"Tháng {m}/{year}",
                    value = data.FirstOrDefault(x => x.month == m)?.revenue ?? 0
                });

            return Json(result);
        }
    }
}
