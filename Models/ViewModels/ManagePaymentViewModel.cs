namespace DoAnChuyenNganh.Models.ViewModels
{
    public class ManagePaymentViewModel
    {
        public string StaffId { get; set; }
        public string StaffName { get; set; }
        public DateTime? LastBillMonth { get; set; }
        public bool? IsPaid { get; set; }

        public bool HasManagedRestaurants { get; set; }
    }
}
