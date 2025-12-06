using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DoAnChuyenNganh.Models
{
    public class User : IdentityUser
    {
        [Required, MaxLength(100)]
        public string? FullName { get; set; }

        [MaxLength(255)]
        public string? Address { get; set; }

        public DateTime? DateOfBirth { get; set; } 

        public bool IsActive { get; set; } = true;

        public bool IsPremium { get; set; } = false; // mặc định là false
        public DateTime? PremiumExpireDate { get; set; }

        public string? LockedByAdminId { get; set; }

        // Relationships
        public ICollection<Reservation>? Reservations { get; set; }
        public ICollection<Review>? Reviews { get; set; }
        public ICollection<Notification>? Notifications { get; set; }
        public ICollection<AIRecommendation>? AIRecommendations { get; set; }
    }
}
