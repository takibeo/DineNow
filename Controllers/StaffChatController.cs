using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class StaffChatController : Controller
{
    private readonly AppDBContext _ctx;
    private readonly UserManager<User> _user;
    private readonly BadWordService _filter;

    public StaffChatController(AppDBContext ctx, UserManager<User> user, BadWordService filter)
    {
        _ctx = ctx;
        _user = user;
        _filter = filter;
    }

    // ===========================
    // CUSTOMER mở chat với Staff
    // ===========================
    public async Task<IActionResult> CustomerChat(int restaurantId)
    {
        var customer = await _user.GetUserAsync(User);
        if (customer == null) return Unauthorized();

        // Lấy Staff phụ trách nhà hàng
        var staffRestaurant = await _ctx.StaffRestaurants
            .Include(sr => sr.User)
            .FirstOrDefaultAsync(sr => sr.RestaurantId == restaurantId);

        if (staffRestaurant == null)
            return NotFound("Chưa có nhân viên hỗ trợ nhà hàng này.");

        var staff = staffRestaurant.User;
        if (!await _user.IsInRoleAsync(staff, "Staff"))
            return BadRequest("Người này không phải Staff.");

        // Room Customer ↔ Staff
        var room = await _ctx.ChatRooms
            .FirstOrDefaultAsync(r => r.CustomerId == customer.Id && r.StaffId == staff.Id);

        if (room == null)
        {
            room = new ChatRoom
            {
                CustomerId = customer.Id,
                StaffId = staff.Id,
                CreatedAt = DateTime.Now
            };
            _ctx.ChatRooms.Add(room);
            await _ctx.SaveChangesAsync();
        }

        return View("CustomerChatStaff", room);
    }

    // ===========================
    // STAFF xem room Customer ↔ Staff
    // ===========================
    public async Task<IActionResult> Staff(int id)
    {
        var staff = await _user.GetUserAsync(User);
        if (staff == null) return Unauthorized();
        if (!await _user.IsInRoleAsync(staff, "Staff")) return Forbid();

        var room = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == id && r.StaffId == staff.Id);

        if (room == null) return NotFound();

        var customerName = await _ctx.Users
            .Where(u => u.Id == room.CustomerId)
            .Select(u => u.FullName ?? u.UserName)
            .FirstOrDefaultAsync();

        ViewBag.CustomerName = customerName ?? room.CustomerId;

        // Khi Staff mở chat → coi như đã đọc tất cả tin nhắn
        room.LastReadByStaff = DateTime.Now;
        await _ctx.SaveChangesAsync();

        return View("StaffChat", room);
    }

    // ===========================
    // SEND tin nhắn Customer ↔ Staff
    // ===========================
    [HttpPost]
    public async Task<IActionResult> Send(int roomId, string msg)
    {
        var me = await _user.GetUserAsync(User);
        if (me == null) return Json(new { ok = false, msg = "Chưa đăng nhập" });

        if (!me.IsActive)
            return Json(new { ok = false, msg = "Tài khoản bị khóa." });

        var room = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null) return Json(new { ok = false, msg = "Phòng không tồn tại" });

        bool isStaff = await _user.IsInRoleAsync(me, "Staff");
        bool isCustomer = !isStaff;

        // Kiểm tra quyền gửi
        if (isStaff && room.StaffId != me.Id) return Json(new { ok = false, msg = "Không có quyền gửi" });
        if (isCustomer && room.CustomerId != me.Id) return Json(new { ok = false, msg = "Không có quyền gửi" });

        // Kiểm tra từ cấm
        bool bad = await _filter.IsBadAsync(msg);
        var m = new Message
        {
            RoomId = roomId,
            UserId = me.Id,
            Content = msg,
            IsBlocked = bad,
            CreatedAt = DateTime.Now
        };
        _ctx.Messages.Add(m);

        // Xử lý cảnh báo nếu Customer
        if (isCustomer && bad)
        {
            me.WarningCount++;
            _ctx.Users.Update(me);
            if (me.WarningCount >= 5)
            {
                me.IsActive = false;
                me.LockedAt = DateTime.Now;
            }
        }

        // Reset Deleted flags
        if (isCustomer && room.DeletedByCustomer) room.DeletedByCustomer = false;
        if (isStaff && room.DeletedByStaff) room.DeletedByStaff = false;

        await _ctx.SaveChangesAsync();

        if (bad) return Json(new { ok = false, msg = "Tin nhắn không hợp lệ!" });

        return Json(new { ok = true, msg = "Đã gửi" });
    }

    // ===========================
    // LOAD tin nhắn Customer ↔ Staff
    // ===========================
    public async Task<IActionResult> Load(int roomId)
    {
        var me = await _user.GetUserAsync(User);
        if (me == null) return Unauthorized();

        var room = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null) return Json(new List<object>());

        bool isStaff = await _user.IsInRoleAsync(me, "Staff");
        bool isCustomer = !isStaff;

        if (isStaff && room.StaffId != me.Id) return Json(new { error = "Forbidden" });
        if (isCustomer && room.CustomerId != me.Id) return Json(new { error = "Forbidden" });

        DateTime? lastDeleted = isStaff ? room.LastDeletedByStaff : room.LastDeletedByCustomer;

        var messages = room.Messages
            .Where(m => lastDeleted == null || m.CreatedAt > lastDeleted)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                userId = m.UserId,
                senderName = _ctx.Users.Where(u => u.Id == m.UserId)
                                       .Select(u => u.FullName ?? u.UserName)
                                       .FirstOrDefault(),
                content = m.Content,
                isBlocked = m.IsBlocked,
                createdAt = m.CreatedAt
            })
            .ToList();

        return Json(messages);
    }

    // ===========================
    // STAFF LIST ROOMS
    // ===========================
    public async Task<IActionResult> StaffRooms()
    {
        var staff = await _user.GetUserAsync(User);
        if (staff == null || !await _user.IsInRoleAsync(staff, "Staff")) return Forbid();

        var rooms = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .Where(r => r.StaffId == staff.Id && !r.DeletedByStaff)
            .ToListAsync();

        var customerIds = rooms.Select(r => r.CustomerId).ToList();
        var users = await _ctx.Users.Where(u => customerIds.Contains(u.Id)).ToListAsync();

        ViewBag.UserNames = users.ToDictionary(
            u => u.Id,
            u => string.IsNullOrEmpty(u.FullName) ? u.Email : u.FullName
        );

        var newMessages = rooms.ToDictionary(
            r => r.Id,
            r => r.Messages.Any(m => m.UserId == r.CustomerId
                                     && (r.LastReadByStaff == null || m.CreatedAt > r.LastReadByStaff)
                                     && (r.LastDeletedByStaff == null || m.CreatedAt > r.LastDeletedByStaff))
        );
        ViewBag.NewMessages = newMessages;

        return View("StaffRooms", rooms);
    }

    // ===========================
    // XÓA tin nhắn 1 phía
    // ===========================
    [HttpPost]
    public async Task<IActionResult> DeleteChat(int id)
    {
        var me = await _user.GetUserAsync(User);
        if (me == null) return Json(new { ok = false, msg = "Chưa đăng nhập" });

        var room = await _ctx.ChatRooms.Include(r => r.Messages)
                        .FirstOrDefaultAsync(r => r.Id == id);
        if (room == null) return Json(new { ok = false, msg = "Phòng chat không tồn tại" });

        bool isStaff = await _user.IsInRoleAsync(me, "Staff");
        bool isCustomer = !isStaff;

        if (isStaff)
        {
            room.DeletedByStaff = true;
            room.LastDeletedByStaff = DateTime.Now;
        }
        else if (isCustomer)
        {
            room.DeletedByCustomer = true;
            room.LastDeletedByCustomer = DateTime.Now;
        }

        await _ctx.SaveChangesAsync();
        return Json(new { ok = true, msg = "Đã xóa tất cả tin nhắn!" });
    }
}
