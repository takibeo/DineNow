using DoAnChuyenNganh.Models;

public class Message
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string UserId { get; set; } // khách hoặc admin
    public string Content { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ChatRoom Room { get; set; }
}
