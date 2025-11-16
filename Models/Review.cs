namespace DoAnChuyenNganh.Models
{
    public class Review
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public int RestaurantId { get; set; }
        public string Comment { get; set; } = "";
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }
        public Restaurant? Restaurant { get; set; }
        public SentimentAnalysisLog? SentimentAnalysis { get; set; }
    }
}
