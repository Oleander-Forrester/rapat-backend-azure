namespace rapat_backend.DTOs.Rapat
{
    public class PesertaRapatDetailDto
    {
        public string? KaryawanId { get; set; }
        public string? Nama { get; set; }
        public string? Username { get; set; }
        public string? Jabatan { get; set; } = string.Empty;
        public string? StatusHadir { get; set; } = string.Empty;
        public string? Peran { get; set; } = string.Empty;
        public string? Keterangan { get; set; } = string.Empty;
        public bool IsNotulis { get; set; }
        public bool IsExternal { get; set; }
        public string? Email { get; set; }
    }
}