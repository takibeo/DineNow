namespace DoAnChuyenNganh.Models
{
    public class PremiumPackageModel
    {
        public string Name { get; set; } = "Gói Premium 30 ngày";
        public string Description { get; set; } = "Trải nghiệm đầy đủ tính năng Premium.";
        public decimal Amount { get; set; } = 100000; // VNĐ
        public int DurationInDays { get; set; } = 30;
        public string OrderType { get; set; } = "premium";
    }
}
