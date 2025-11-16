namespace DoAnChuyenNganh.Models
{
    public class UserLog
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string Action { get; set; }
        public string? Description { get; set; }   // Chi tiết hơn (VD: "Đăng nhập thành công")
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public User? User { get; set; }            // Navigation để Include()
    }
}
