namespace DoAnChuyenNganh.Models
{
    public class SentimentAnalysisLog
    {
        public int Id { get; set; }
        public int ReviewId { get; set; }
        public double SentimentScore { get; set; }
        public string SentimentLabel { get; set; } = ""; // Positive | Neutral | Negative
        public DateTime AnalyzedAt { get; set; } = DateTime.Now;

        public Review? Review { get; set; }
    }
}
