namespace DoAnChuyenNganh.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int ReservationId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // Cash | Card | Online
        public string Status { get; set; } = "Pending"; // Pending | Paid | Failed
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        public Reservation? Reservation { get; set; }
    }
}
