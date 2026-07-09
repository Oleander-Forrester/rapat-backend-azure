namespace rapat_backend.DTOs.Rapat
{
    public class RapatDetailDto
    {
        public int Id { get; set; }
        public string Judul { get; set; } = string.Empty;
        public string? EventId { get; set; }
        public string Jenis { get; set; } = string.Empty;
        public int? RuanganId { get; set; }
        public string RuanganNama { get; set; } = string.Empty;
        public string? NamaRuanganManual { get; set; }
        public DateTime WaktuMulai { get; set; }
        public DateTime WaktuSelesai { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PembuatUsername { get; set; } = string.Empty;
        public string PembuatNama { get; set; } = string.Empty;
        public string PembuatEmail { get; set; } = string.Empty;
        public string? LinkMeeting { get; set; }
        public string? IsiNotulensi { get; set; }
        public string? FileDokumentasi { get; set; }

        public List<PesertaRapatDetailDto> Peserta { get; set; } = new List<PesertaRapatDetailDto>();
        public List<ItemAksiRapatDetailDto> ItemAksi { get; set; } = new List<ItemAksiRapatDetailDto>();
    }
}
