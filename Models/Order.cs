namespace DoAnChuyenNganh.Models
{
    public class Order
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        public User? User { get; set; }

        public int RestaurantId { get; set; }
        public Restaurant? Restaurant { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal AdminCommission { get; set; }

        public string Status { get; set; } = "Pending";
        public string? PaymentMethod { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<OrderItem> Items { get; set; } = new();
    }
}
