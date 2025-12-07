namespace DoAnChuyenNganh.Models
{
    public class ChatRoom
    {
        public int Id { get; set; }
        public string CustomerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool DeletedByAdmin { get; set; } = false;
        public bool DeletedByCustomer { get; set; } = false;

        // Thời điểm xóa cuối cùng, để ẩn tin nhắn cũ
        public DateTime? LastDeletedByAdmin { get; set; }
        public DateTime? LastDeletedByCustomer { get; set; }
        public DateTime? LastReadByAdmin { get; set; }  // chỉ để badge, không ảnh hưởng tin nhắn

        public ICollection<Message> Messages { get; set; }
    }
}
