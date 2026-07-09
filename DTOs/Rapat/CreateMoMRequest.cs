using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace rapat_backend.DTOs.Rapat
{
    public class CreateMoMRequest
    {
        [Required(ErrorMessage = "ID Rapat harus diisi.")]
        public int RapatId { get; set; }
        public string? IsiNotulensi { get; set; } = string.Empty;
        
        public IFormFile? FileDokumentasi { get; set; }

        public string? Tempat { get; set; }
        public string? Tanggal { get; set; }
    }
}
