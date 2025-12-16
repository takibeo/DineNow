namespace DoAnChuyenNganh.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        // FK tới đơn hàng
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        // FK tới món ăn
        public int MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        public int Quantity { get; set; }

        // Giá tại thời điểm đặt
        public decimal Price { get; set; }
    }
}
