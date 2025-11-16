using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DoAnChuyenNganh.Models
{
    public class Reservation
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public int RestaurantId { get; set; }
        public DateTime ReservationDate { get; set; }
        public int NumberOfGuests { get; set; }
        public string Status { get; set; } = "Pending"; // Pending | Confirmed | Cancelled
        public string? Note { get; set; }
        [Display(Name = "Số điện thoại liên hệ")]
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại liên hệ")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string? ContactPhone { get; set; }

        // Relationships
        public User? User { get; set; }
        public Restaurant? Restaurant { get; set; }
        public Payment? Payment { get; set; } 
    }
}
