using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DoAnChuyenNganh.Models
{
    public class Restaurant
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Description { get; set; } = "";
        public string OpenHours { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string CuisineType { get; set; } = ""; // Loại món
        [Range(0, 1000000)]
        public decimal AveragePrice { get; set; }     // Giá trung bình

        public bool IsApproved { get; set; } = false;

        // Relationships
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<MenuItem>? MenuItems { get; set; }
        public ICollection<Reservation>? Reservations { get; set; }
        public ICollection<Review>? Reviews { get; set; }
    }
}
