using System;
using DoAnChuyenNganh.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DoAnChuyenNganh.Data
{
    public class AppDBContext : IdentityDbContext<User>
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        public DbSet<Restaurant> Restaurants { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AIRecommendation> AIRecommendations { get; set; }
        public DbSet<SentimentAnalysisLog> SentimentAnalysisLogs { get; set; }

        public DbSet<UserLog> UserLogs { get; set; }
        public DbSet<StaffRestaurant> StaffRestaurants { get; set; }

        public DbSet<FavoriteRestaurant> FavoriteRestaurants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1:1 Reservation - Payment
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Payment)
                .WithOne(p => p.Reservation)
                .HasForeignKey<Payment>(p => p.ReservationId);

            // 1:1 Review - SentimentAnalysisLog
            modelBuilder.Entity<Review>()
                .HasOne(r => r.SentimentAnalysis)
                .WithOne(s => s.Review)
                .HasForeignKey<SentimentAnalysisLog>(s => s.ReviewId);

            // 1:N Restaurant - MenuItem
            modelBuilder.Entity<Restaurant>()
                .HasMany(r => r.MenuItems)
                .WithOne(m => m.Restaurant)
                .HasForeignKey(m => m.RestaurantId);

            // 1:N Restaurant - Reservation
            modelBuilder.Entity<Restaurant>()
                .HasMany(r => r.Reservations)
                .WithOne(rs => rs.Restaurant)
                .HasForeignKey(rs => rs.RestaurantId);

            modelBuilder.Entity<MenuItem>()
                .Property(m => m.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");
        }
    }
}
