using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models; // để dùng BillingStatus
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly AppDBContext _context;

        public ReportController(AppDBContext context)
        {
            _context = context;
        }

        // Trang chọn loại báo cáo
        public IActionResult Index()
        {
            return View();
        }

        // Trang báo cáo đặt bàn đã xác nhận
        public async Task<IActionResult> ConfirmedReservations()
        {
            ViewBag.Restaurants = await _context.Restaurants.ToListAsync();
            return View();
        }

        // API dữ liệu chart đặt bàn đã xác nhận
        [HttpGet]
        public async Task<IActionResult> GetConfirmedReservationData(
            int? restaurantId,
            string groupBy,
            int? month,
            int? year)
        {
            var query = _context.Reservations
                .Where(r => r.Status == "Confirmed");

            if (restaurantId.HasValue)
                query = query.Where(r => r.RestaurantId == restaurantId);

            year ??= DateTime.Now.Year;

            // ================= DAY =================
            if (groupBy == "Day")
            {
                if (!month.HasValue)
                    return BadRequest("Thiếu tháng");

                var daysInMonth = DateTime.DaysInMonth(year.Value, month.Value);

                var data = await query
                    .Where(r => r.ReservationDate.Year == year
                             && r.ReservationDate.Month == month)
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

            // ================= MONTH =================
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

            // ================= YEAR =================
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

        // API dữ liệu chart doanh thu
        [HttpGet]
        public async Task<IActionResult> GetRevenueData(
    string groupBy,
    int? year)
        {
            year ??= DateTime.Now.Year;

            // ================== HOA HỒNG ĐƠN ĐỒ ĂN ==================
            var foodQuery = _context.Orders
                .Where(o => o.Status == "Confirmed");

            // ================== PHÍ STAFF ==================
            var staffQuery = _context.StaffBillings
                .Where(b => b.Status == BillingStatus.Accepted);

            // ================== THEO THÁNG ==================
            if (groupBy == "Month")
            {
                var foodData = await foodQuery
                    .Where(o => o.CreatedAt.Year == year)
                    .GroupBy(o => o.CreatedAt.Month)
                    .Select(g => new
                    {
                        month = g.Key,
                        total = g.Sum(x => x.AdminCommission)
                    })
                    .ToListAsync();

                var staffData = await staffQuery
                    .Where(b => b.Month.Year == year)
                    .GroupBy(b => b.Month.Month)
                    .Select(g => new
                    {
                        month = g.Key,
                        total = g.Sum(x => x.TotalFee)
                    })
                    .ToListAsync();

                var result = Enumerable.Range(1, 12)
                    .Select(m => new
                    {
                        label = $"Tháng {m}/{year}",
                        value =
                            (foodData.FirstOrDefault(x => x.month == m)?.total ?? 0)
                          + (staffData.FirstOrDefault(x => x.month == m)?.total ?? 0)
                    });

                return Json(result);
            }

            // ================== THEO NĂM ==================
            var foodYears = await foodQuery
                .GroupBy(o => o.CreatedAt.Year)
                .Select(g => new
                {
                    year = g.Key,
                    total = g.Sum(x => x.AdminCommission)
                })
                .ToListAsync();

            var staffYears = await staffQuery
                .GroupBy(b => b.Month.Year)
                .Select(g => new
                {
                    year = g.Key,
                    total = g.Sum(x => x.TotalFee)
                })
                .ToListAsync();

            var years = foodYears
                .Select(x => x.year)
                .Union(staffYears.Select(x => x.year))
                .OrderBy(y => y)
                .Select(y => new
                {
                    label = y.ToString(),
                    value =
                        (foodYears.FirstOrDefault(x => x.year == y)?.total ?? 0)
                      + (staffYears.FirstOrDefault(x => x.year == y)?.total ?? 0)
                });

            return Json(years);
        }

        // Trang báo cáo doanh thu
        public IActionResult RevenueReport()
        {
            return View();
        }
    }
}
