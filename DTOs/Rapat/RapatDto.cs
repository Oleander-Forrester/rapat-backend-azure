namespace rapat_backend.DTOs.Rapat
{
    public class RapatDto
    {
        public int Id { get; set; }
        public string Judul { get; set; } = string.Empty;
        public string Jenis { get; set; } = string.Empty;
        public string RuanganNama { get; set; } = string.Empty;
        public DateTime WaktuMulai { get; set; }
        public DateTime WaktuSelesai { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PembuatNama { get; set; } = string.Empty;
        public string PembuatUsername { get; set; } = string.Empty;
        public string? LinkMeeting { get; set; }
    }
}
