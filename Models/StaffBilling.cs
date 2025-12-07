using System;
using System.ComponentModel.DataAnnotations;

namespace DoAnChuyenNganh.Models
{
    public class StaffBilling
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } // Staff

        [Required]
        public DateTime Month { get; set; } // Tháng tính phí

        public int ManagedRestaurantCount { get; set; } // Số nhà hàng Staff quản lý
        public decimal RestaurantFee { get; set; }      // 300k * số nhà hàng

        public int ReservationCount { get; set; }       // Số lượt đặt bàn
        public decimal ReservationFee { get; set; }     // 5k * số lượt đặt bàn

        public decimal TotalFee { get; set; }           // Tổng phí
        public bool IsPaid { get; set; } = false;      // Thanh toán chưa
    }
}
