using System.ComponentModel.DataAnnotations;

namespace rapat_backend.DTOs.Rapat
{
    public class UpdateAbsensiRapatRequest
    {
        [Required(ErrorMessage = "RapatId harus diisi.")]
        public int RapatId { get; set; }
        public string? KaryawanId { get; set; }
        public string? Email { get; set; }

        [Required(ErrorMessage = "StatusHadir harus diisi.")]
        public string StatusHadir { get; set; } = string.Empty;
        
        public string? Keterangan { get; set; }
        public string? PeranBaru { get; set; }
    }
}