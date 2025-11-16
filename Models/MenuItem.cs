namespace DoAnChuyenNganh.Models
{
    public class MenuItem
    {
        public int Id { get; set; }
        public int RestaurantId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string Category { get; set; } = "Khác";  // Mặc định là "Khác"

        public Restaurant? Restaurant { get; set; }
    }
}
