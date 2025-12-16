namespace DoAnChuyenNganh.Models.ViewModels
{
    public class ConfirmedReservationReportVM
    {
        public string GroupType { get; set; } // Day / Month / Year
        public List<ConfirmedReservationItem> Data { get; set; } = new();
    }

    public class ConfirmedReservationItem
    {
        public string Label { get; set; } // Ngày / Tháng / Năm
        public int Count { get; set; }
    }

}
