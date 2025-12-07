using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using Microsoft.EntityFrameworkCore;

public class CollaborativeFilteringService
{
    private readonly AppDBContext _ctx;

    public CollaborativeFilteringService(AppDBContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<List<Restaurant>> GetRecommendationsAsync(string userId, int take = 5)
    {
        var userReviews = await _ctx.Reviews
            .Where(r => r.UserId == userId)
            .ToListAsync();

        if (userReviews.Count == 0)
            return new();

        var allReviews = await _ctx.Reviews.ToListAsync();

        var users = allReviews.Select(r => r.UserId).Distinct();

        var similarity = new Dictionary<string, double>();

        foreach (var u in users)
        {
            if (u == userId) continue;

            similarity[u] = Cosine(userReviews,
                allReviews.Where(r => r.UserId == u).ToList()
            );
        }

        var bestUser = similarity
            .OrderByDescending(x => x.Value)
            .FirstOrDefault().Key;

        if (bestUser == null) return new();

        var rec = await _ctx.Reviews
            .Where(r => r.UserId == bestUser)
            .OrderByDescending(r => r.Rating)
            .Take(take)
            .Join(_ctx.Restaurants,
                  r => r.RestaurantId,
                  rest => rest.Id,
                  (r, rest) => rest)
            .ToListAsync();

        return rec;
    }


    private double Cosine(List<Review> a, List<Review> b)
    {
        var common = a.Select(x => x.RestaurantId)
                      .Intersect(b.Select(x => x.RestaurantId));

        double dot = 0, na = 0, nb = 0;

        foreach (var id in common)
        {
            var ra = a.First(x => x.RestaurantId == id).Rating;
            var rb = b.First(x => x.RestaurantId == id).Rating;

            dot += ra * rb;
            na += ra * ra;
            nb += rb * rb;
        }

        if (na == 0 || nb == 0) return 0;

        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
