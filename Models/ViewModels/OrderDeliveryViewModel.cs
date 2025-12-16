using System.ComponentModel.DataAnnotations;

namespace DoAnChuyenNganh.Models.ViewModels
{
    public class OrderDeliveryViewModel
    {
        public int OrderId { get; set; }

        [Display(Name = "Địa chỉ giao hàng")]
        public string DeliveryAddress { get; set; } = "";

        [Display(Name = "Số điện thoại liên lạc")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; } = "";

        [Display(Name = "Ghi chú")]
        public string Note { get; set; } = "";
    }
}
