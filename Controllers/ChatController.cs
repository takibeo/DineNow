using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class ChatController : Controller
{
    private readonly AppDBContext _ctx;
    private readonly UserManager<User> _user;
    private readonly BadWordService _filter;

    public ChatController(AppDBContext ctx, UserManager<User> user, BadWordService filter)
    {
        _ctx = ctx;
        _user = user;
        _filter = filter;
    }

    // ===========================
    // CUSTOMER mở chat
    // ===========================
    public async Task<IActionResult> Customer()
    {
        var me = await _user.GetUserAsync(User);
        if (me == null) return Unauthorized();

        var room = await _ctx.ChatRooms
            .FirstOrDefaultAsync(r => r.CustomerId == me.Id);

        if (room == null)
        {
            room = new ChatRoom
            {
                CustomerId = me.Id,
                CreatedAt = DateTime.Now
            };
            _ctx.ChatRooms.Add(room);
            await _ctx.SaveChangesAsync();
        }

        return View(room);
    }

    // ===========================
    // ADMIN xem chat
    // ===========================
    public async Task<IActionResult> Admin(int id)
    {
        var me = await _user.GetUserAsync(User);
        if (me == null) return Unauthorized();
        if (!await _user.IsInRoleAsync(me, "Admin")) return Forbid();

        var room = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null) return NotFound();

        var customerName = await _ctx.Users
            .Where(u => u.Id == room.CustomerId)
            .Select(u => u.FullName ?? u.UserName)
            .FirstOrDefaultAsync();

        ViewBag.CustomerName = customerName ?? room.CustomerId;

        // Khi Admin mở chat → coi như đã đọc tất cả tin nhắn
        room.LastReadByAdmin = DateTime.Now;
        await _ctx.SaveChangesAsync();

        return View(room);
    }

    // ===========================
    // SEND TIN NHẮN
    // ===========================
    [HttpPost]
    public async Task<IActionResult> Send(int roomId, string msg)
    {
        var me = await _user.GetUserAsync(User);
        if (me == null) return Json(new { ok = false, msg = "Chưa đăng nhập" });

        if (!me.IsActive)
            return Json(new { ok = false, msg = "Tài khoản của bạn đã bị khóa 1 ngày do vi phạm." });

        var room = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null) return Json(new { ok = false, msg = "Phòng không tồn tại" });

        bool isAdmin = await _user.IsInRoleAsync(me, "Admin");

        if (!isAdmin && room.CustomerId != me.Id)
            return Json(new { ok = false, msg = "Không có quyền gửi tin" });

        // Kiểm tra từ cấm
        bool bad = await _filter.IsBadAsync(msg);
        Console.WriteLine($"[BadWordService Test] User: {me.UserName}, Tin nhắn: '{msg}', Bad? {bad}");
        var m = new Message
        {
            RoomId = roomId,
            UserId = me.Id,
            Content = msg,
            IsBlocked = bad,
            CreatedAt = DateTime.Now
        };

        _ctx.Messages.Add(m);

        // Xử lý cảnh báo nếu là Customer
        if (!isAdmin && bad)
        {
            me.WarningCount++; // tăng số cảnh báo
            _ctx.Users.Update(me);

            // Khóa tài khoản nếu quá 5 lần cảnh báo trong 24h
            if (me.WarningCount >= 5)
            {
                me.IsActive = false;
                me.LockedAt = DateTime.Now; // thêm trường LockedAt để mở lại sau 1 ngày
            }
        }

        // Reset Deleted flags
        if (!isAdmin && room.DeletedByAdmin) room.DeletedByAdmin = false;
        if (isAdmin && room.DeletedByCustomer) room.DeletedByCustomer = false;

        await _ctx.SaveChangesAsync();

        if (bad)
            return Json(new { ok = false, msg = "Tin nhắn không hợp lệ!" });

        return Json(new { ok = true, msg = "Đã gửi" });
    }
    // ===========================
    // LOAD TIN NHẮN
    // ===========================
    public async Task<IActionResult> Load(int roomId)
    {
        var me = await _user.GetUserAsync(User);
        if (me == null) return Unauthorized();

        var room = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null) return Json(new List<object>());

        bool isAdmin = await _user.IsInRoleAsync(me, "Admin");

        if (!isAdmin && room.CustomerId != me.Id)
            return Json(new { error = "Forbidden" });

        // Lấy thời điểm xóa cuối cùng
        DateTime? lastDeleted = isAdmin ? room.LastDeletedByAdmin : room.LastDeletedByCustomer;

        var messages = room.Messages
            .Where(m => lastDeleted == null || m.CreatedAt > lastDeleted) // chỉ ẩn tin nhắn cũ
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
    // ADMIN LIST ROOMS
    // ===========================
    public async Task<IActionResult> Rooms()
    {
        var me = await _user.GetUserAsync(User);
        if (!await _user.IsInRoleAsync(me, "Admin")) return Forbid();

        var rooms = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .Where(r => !r.DeletedByAdmin) // ẩn room Admin đã xóa
            .ToListAsync();

        var ids = rooms.Select(r => r.CustomerId).ToList();
        var users = await _ctx.Users.Where(u => ids.Contains(u.Id)).ToListAsync();

        ViewBag.UserNames = users.ToDictionary(
            u => u.Id,
            u => string.IsNullOrEmpty(u.FullName) ? u.Email : u.FullName
        );

        // Tính tin nhắn mới cho Admin
        var newMessages = rooms.ToDictionary(
            r => r.Id,
            r => r.Messages.Any(m => m.UserId == r.CustomerId // chỉ tính tin nhắn của Customer
                                     && (r.LastReadByAdmin == null || m.CreatedAt > r.LastReadByAdmin)
                                     && (r.LastDeletedByAdmin == null || m.CreatedAt > r.LastDeletedByAdmin)
                   )
        );
        ViewBag.NewMessages = newMessages;

        return View(rooms);
    }

    // ===========================
    // XÓA TIN NHẮN 1 PHÍA
    // ===========================
    public async Task<IActionResult> DeleteChat(int id)
    {
        var me = await _user.GetUserAsync(User);
        if (me == null) return Unauthorized();

        var room = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null) return NotFound();

        bool isAdmin = await _user.IsInRoleAsync(me, "Admin");

        if (isAdmin)
        {
            // Ẩn tin nhắn với Admin, Customer vẫn thấy
            room.DeletedByAdmin = true;
            room.LastDeletedByAdmin = DateTime.Now;
        }
        else
        {
            // Ẩn tin nhắn với Customer, Admin vẫn thấy
            room.DeletedByCustomer = true;
            room.LastDeletedByCustomer = DateTime.Now;
        }

        await _ctx.SaveChangesAsync();

        return isAdmin ? RedirectToAction("Rooms") : RedirectToAction("Customer");
    }
}
