using System;
using DoAnChuyenNganh.Models; // để dùng BillingStatus

namespace DoAnChuyenNganh.Models.ViewModels
{
    public class ManagePaymentViewModel
    {
        public string StaffId { get; set; }
        public string StaffName { get; set; }
        public DateTime? LastBillMonth { get; set; }

        // Thay IsPaid bằng BillingStatus để hiển thị trạng thái chính xác
        public BillingStatus? Status { get; set; }

        public bool HasManagedRestaurants { get; set; }

        // Tùy chọn: property tiện lợi cho view
        public bool IsPaid => Status == BillingStatus.Accepted;
        public bool IsPending => Status == BillingStatus.Pending;
        public bool IsRejected => Status == BillingStatus.Rejected;
    }
}
