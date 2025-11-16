namespace DoAnChuyenNganh.Models
{
    public class AIRecommendation
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public int RestaurantId { get; set; }
        public double Score { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }
        public Restaurant? Restaurant { get; set; }
    }
}
