namespace rapat_backend.DTOs.Rapat
{
    public class FilterRapatRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string Search { get; set; } = "";
        public string Status { get; set; } = "";
        public string Sort { get; set; } = "rap_waktu_mulai desc";
        public string? Jenis { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}