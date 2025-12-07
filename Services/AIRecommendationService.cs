using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Services
{
    public class AIRecommendationService
    {
        private readonly AppDBContext _context;

        public AIRecommendationService(AppDBContext context)
        {
            _context = context;
        }

        // Sinh recommendation cho user dựa trên review + sentiment
        public async Task GenerateForUserAsync(string userId)
        {
            var reviews = await _context.Reviews
                .Include(r => r.SentimentAnalysis)
                .Where(r => r.UserId == userId)
                .ToListAsync();

            if (!reviews.Any()) return;

            // Xóa recommendation cũ
            var old = _context.AIRecommendations.Where(x => x.UserId == userId);
            _context.AIRecommendations.RemoveRange(old);

            var results = new List<AIRecommendation>();

            foreach (var r in reviews)
            {
                // Convert label → số
                double sentimentValue = r.SentimentAnalysis?.SentimentLabel switch
                {
                    "POSITIVE" => 0.9,
                    "NEGATIVE" => 0.1,
                    _ => 0.5
                };

                double score = r.Rating * 0.6 + sentimentValue * 0.4;

                results.Add(new AIRecommendation
                {
                    UserId = userId,
                    RestaurantId = r.RestaurantId,
                    Score = score
                });
            }

            await _context.AIRecommendations.AddRangeAsync(results);
            await _context.SaveChangesAsync();
        }

        // Lấy top nhà hàng gợi ý
        public async Task<List<Restaurant>> GetRecommendedAsync(string userId, int take = 5)
        {
            return await _context.AIRecommendations
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.Score)
                .Take(take)
                .Include(x => x.Restaurant)
                .Select(x => x.Restaurant!)
                .ToListAsync();
        }
    }
}
