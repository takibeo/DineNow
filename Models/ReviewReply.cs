namespace DoAnChuyenNganh.Models
{
    public class ReviewReply
    {
        public int Id { get; set; }
        public int ReviewId { get; set; }
        public string UserId { get; set; } = "";
        public string ReplyText { get; set; } = "";
        public DateTime ReplyAt { get; set; } = DateTime.Now;

        public User? User { get; set; }
        public Review? Review { get; set; }
    }
}
