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

        var room = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null) return Json(new { ok = false, msg = "Phòng không tồn tại" });

        bool isAdmin = await _user.IsInRoleAsync(me, "Admin");

        // Kiểm tra quyền gửi
        if (!isAdmin && room.CustomerId != me.Id)
            return Json(new { ok = false, msg = "Không có quyền gửi tin" });

        // Lọc từ cấm
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

        // Nếu Customer gửi, reset DeletedByAdmin flag để Admin vẫn thấy room
        if (!isAdmin && room.DeletedByAdmin)
            room.DeletedByAdmin = false;

        // Nếu Admin gửi, reset DeletedByCustomer flag để Customer vẫn thấy room
        if (isAdmin && room.DeletedByCustomer)
            room.DeletedByCustomer = false;

        await _ctx.SaveChangesAsync();

        return Json(new { ok = !bad, msg = bad ? "Tin nhắn chứa từ cấm!" : "Đã gửi" });
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
    [HttpPost]
    public async Task<IActionResult> DeleteChat(int id)
    {
        var me = await _user.GetUserAsync(User);
        if (me == null) return Json(new { ok = false, msg = "Chưa đăng nhập!" });

        var room = await _ctx.ChatRooms
            .Include(r => r.Messages)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null) return Json(new { ok = false, msg = "Phòng chat không tồn tại!" });

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

        return Json(new { ok = true, msg = "Đã xóa tất cả tin nhắn!" });
    }
}
