namespace DoAnChuyenNganh.Models
{
    public class FavoriteRestaurant
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int RestaurantId { get; set; }

        public User User { get; set; }
        public Restaurant Restaurant { get; set; }
    }
}
