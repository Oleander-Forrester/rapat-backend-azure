using System.ComponentModel.DataAnnotations;

namespace rapat_backend.DTOs.Rapat
{
    public class CreateRapatRequest
    {
        [Required(ErrorMessage = "Judul rapat harus diisi.")]
        [StringLength(200)]
        public string Judul { get; set; } = string.Empty;

        [Required(ErrorMessage = "Jenis rapat harus diisi.")]
        public string Jenis { get; set; } = string.Empty;
        public string? NamaRuanganManual { get; set; }

        [Required(ErrorMessage = "Ruangan harus diisi.")]
        public int RuanganId { get; set; }

        [Required(ErrorMessage = "Waktu mulai harus diisi.")]
        public DateTime WaktuMulai { get; set; }

        [Required(ErrorMessage = "Waktu selesai harus diisi.")]
        public DateTime WaktuSelesai { get; set; }

        [Required(ErrorMessage = "Mode harus diisi (Online/Offline).")]
        [StringLength(20)]
        public string Mode { get; set; } = string.Empty;

        public string? Link { get; set; } = null;

        public List<string> PesertaKaryawanIds { get; set; } = [];

        public List<PesertaExternalRequest>? PesertaExternal { get; set; } = new();

        public bool AutoCreateTeamsLink { get; set; }
    }
}
