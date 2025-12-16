namespace DoAnChuyenNganh.Models
{
    public class ChatRoom
    {
        public int Id { get; set; }

        public string CustomerId { get; set; }
        public string StaffId { get; set; }  // mới: room dành cho Staff

        public bool DeletedByCustomer { get; set; }
        public bool DeletedByAdmin { get; set; }
        public bool DeletedByStaff { get; set; } // trạng thái xóa cho staff

        public DateTime? LastDeletedByCustomer { get; set; }
        public DateTime? LastDeletedByAdmin { get; set; }
        public DateTime? LastDeletedByStaff { get; set; }

        public DateTime? LastReadByAdmin { get; set; }
        public DateTime? LastReadByStaff { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<Message> Messages { get; set; }
    }
}
