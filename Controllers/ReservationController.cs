using System.Security.Claims;
using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using DoAnChuyenNganh.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Controllers
{
    [Authorize]
    public class ReservationController : Controller
    {
        private readonly AppDBContext _context;
        private readonly IEmailSender _emailSender;

        public ReservationController(AppDBContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        // ✅ Ghi log người dùng (đồng nhất với AccountController)
        private async Task SaveUserLog(string userId, string action, string? description = null)
        {
            var log = new UserLog
            {
                UserId = userId,
                Action = action,
                Description = description ?? $"{action} thành công",
                Timestamp = DateTime.Now
            };
            _context.UserLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        private async Task SaveNotification(string userId, string message)
        {
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

        // --- Tạo đặt bàn ---
        [HttpGet]
        [Authorize]
        public IActionResult Create(int? restaurantId)
        {
            ViewBag.Restaurants = _context.Restaurants.ToList();

            var reservation = new Reservation();
            if (restaurantId.HasValue)
                reservation.RestaurantId = restaurantId.Value;

            return View(reservation);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Reservation reservation)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Restaurants = _context.Restaurants.ToList();
                return View(reservation);
            }

            // ✅ Server-side check: giờ đặt không được nhỏ hơn hiện tại
            /*if (reservation.ReservationDate < DateTime.Now)
            {
                return Json(new
                {
                    success = false,
                    message = "Không thể đặt bàn ở giờ đã trôi qua!"
                });
            }*/

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            reservation.UserId = userId;

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            var restaurant = await _context.Restaurants.FindAsync(reservation.RestaurantId);
            string restaurantName = restaurant?.Name ?? "Nhà hàng";

            string timeText = reservation.ReservationDate.ToString("HH:mm dd/MM/yyyy");

            // ================================
            // 🔔 1) THÔNG BÁO CHO CUSTOMER
            // ================================
            await SaveNotification(
                userId,
                $"Bạn đã đặt bàn tại {restaurantName} lúc {timeText}. "
            );

            // ================================
            // 🔔 2) THÔNG BÁO CHO STAFF LIÊN QUAN
            // ================================
            var staffList = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == reservation.RestaurantId)
                .Select(sr => sr.UserId)
                .ToListAsync();

            foreach (var staffId in staffList)
            {
                await SaveNotification(
                    staffId,
                    $"Khách hàng đã tạo đơn đặt bàn tại {restaurantName} lúc {timeText}."
                );
            }

            return Json(new
            {
                success = true,
                message = $"Bạn đã đặt bàn thành công tại {restaurantName}! Vui lòng chờ xác nhận từ nhân viên. Bạn sẽ nhận được Email về đơn đặt bàn của bạn sau khi đã xác nhận."
            });
        }

        // --- Danh sách đặt bàn cá nhân ---
        [Authorize]
        public async Task<IActionResult> MyReservations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reservations = await _context.Reservations
                .Include(r => r.Restaurant)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.ReservationDate)
                .ToListAsync();

            return View(reservations);
        }

        // --- Quản lý (Admin, Staff) ---
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Manage()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            IQueryable<Reservation> query = _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Restaurant)
                .OrderByDescending(r => r.ReservationDate);

            if (User.IsInRole("Staff"))
            {
                var staffRestaurants = await _context.StaffRestaurants
                    .Where(sr => sr.UserId == userId)
                    .Select(sr => sr.RestaurantId)
                    .ToListAsync();

                if (staffRestaurants.Any())
                    query = query.Where(r => staffRestaurants.Contains(r.RestaurantId));
                else
                    query = query.Where(r => false);
            }

            var reservations = await query.ToListAsync();
            return View(reservations);
        }

        // --- Cập nhật trạng thái ---
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var reservation = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Restaurant)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reservation == null) return NotFound();

            reservation.Status = status;
            await _context.SaveChangesAsync();

            string restaurantName = reservation.Restaurant?.Name ?? $"ID {reservation.RestaurantId}";
            string statusText = status switch
            {
                "Confirmed" => "được chấp nhận ✔️",
                "Cancelled" => "bị từ chối ❌",
                _ => $"được cập nhật thành {status}"
            };

            // ================================
            // 📢 TẠO THÔNG BÁO CHO CUSTOMER
            // ================================
            string message =
                $"Đơn đặt bàn tại {restaurantName} lúc {reservation.ReservationDate:HH:mm dd/MM/yyyy} {statusText}.";

            var notification = new Notification
            {
                UserId = reservation.UserId,
                Message = message,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // ================================
            // 📧 GIỮ NGUYÊN PHẦN GỬI EMAIL
            // ================================
            if (reservation.User?.Email != null)
            {
                string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/email-templates/reservation.cshtml");
                string template = System.IO.File.ReadAllText(templatePath);

                string emailBody = template
                    .Replace("{TITLE}", status == "Confirmed" ? "Đặt bàn đã được xác nhận!" : "Đặt bàn đã bị từ chối")
                    .Replace("{FULL_NAME}", reservation.User.FullName)
                    .Replace("{RESTAURANT_NAME}", restaurantName)
                    .Replace("{RES_DATE}", reservation.ReservationDate.ToString("HH:mm dd/MM/yyyy"))
                    .Replace("{GUESTS}", reservation.NumberOfGuests.ToString())
                    .Replace("{STATUS_TEXT}", statusText)
                    .Replace("{STATUS_COLOR}", status == "Confirmed" ? "#2E7D32" : "#C62828")
                    .Replace("{MESSAGE_LINE_1}",
                        status == "Confirmed"
                            ? "Đơn đặt bàn của bạn đã được <strong>chấp nhận</strong> 🎉"
                            : "Rất tiếc, đơn đặt bàn của bạn đã <strong>bị từ chối</strong> ❌")
                    .Replace("{MESSAGE_LINE_2}",
                        status == "Confirmed"
                            ? "Chúng tôi rất hân hạnh được phục vụ bạn."
                            : "Vui lòng đặt lại thời gian khác hoặc liên hệ nhà hàng để biết thêm thông tin.");

                string subject = status == "Confirmed"
                    ? "Đặt bàn của bạn đã được xác nhận!"
                    : "Đặt bàn của bạn đã bị từ chối";

                await _emailSender.SendEmailAsync(reservation.User.Email, subject, emailBody);
            }

            TempData["Success"] = $"Đã cập nhật trạng thái đơn #{reservation.Id}: {statusText}.";
            return RedirectToAction("Manage");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> GuestCancel(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var reservation = await _context.Reservations
                .Include(r => r.Restaurant)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (reservation == null)
                return NotFound();

            if (reservation.Status != "Pending" && reservation.Status != "Confirmed")
                return BadRequest("Không thể hủy đơn ở trạng thái hiện tại.");

            reservation.Status = "GuestCancelled";
            await _context.SaveChangesAsync();

            string restaurantName = reservation.Restaurant?.Name ?? "Nhà hàng";

            // 🔔 Thông báo cho Customer
            await SaveNotification(
                userId,
                $"Bạn đã hủy đơn đặt bàn tại {restaurantName}."
            );

            // 🔔 Gửi Notification cho tất cả Staff quản lý nhà hàng
            var staffList = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == reservation.RestaurantId)
                .Select(sr => sr.UserId)
                .ToListAsync();

            foreach (var staffId in staffList)
            {
                await SaveNotification(
                    staffId,
                    $"Khách đã hủy đơn đặt bàn tại {restaurantName}."
                );
            }

            return Json(new { success = true });
        }
    }
}
