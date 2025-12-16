namespace DoAnChuyenNganh.Models.ViewModels
{
    public class OrderDetailViewModel
    {
        public Order Order { get; set; } = new();
        public OrderDeliveryViewModel DeliveryInfo { get; set; } = new();
    }
}
