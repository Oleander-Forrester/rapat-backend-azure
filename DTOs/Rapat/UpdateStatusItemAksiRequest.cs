using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace rapat_backend.DTOs.Rapat
{
    public class UpdateStatusItemAksiRequest
    {
        [Required(ErrorMessage = "ID Tindak Lanjut harus diisi.")]
        public int TindakLanjutId { get; set; }

        [Required(ErrorMessage = "Status baru harus diisi.")]
        [StringLength(20)]
        public string StatusBaru { get; set; } = string.Empty;

        public string? FilePath { get; set; }

        public IFormFile? FileBukti { get; set; }
    }
}
