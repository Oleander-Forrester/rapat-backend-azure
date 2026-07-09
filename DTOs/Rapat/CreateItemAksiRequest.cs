using System.ComponentModel.DataAnnotations;

namespace rapat_backend.DTOs.Rapat
{
    public class CreateItemAksiRequest
    {
        [Required(ErrorMessage = "ID Rapat harus diisi.")]
        public int RapatId { get; set; }

        [Required(ErrorMessage = "Deskripsi Item Aksi harus diisi.")]
        public string Deskripsi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Deadline harus diisi.")]
        public DateTime Deadline { get; set; }

        [Required(ErrorMessage = "Karyawan yang ditugaskan harus dipilih.")]
        public string KaryawanIdDitugaskan { get; set; } = string.Empty;
    }
}