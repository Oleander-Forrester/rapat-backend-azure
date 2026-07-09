using System.ComponentModel.DataAnnotations;

namespace rapat_backend.DTOs.Rapat
{
    public class UpdateStatusRapatRequest
    {
        [Required(ErrorMessage = "Status harus diisi.")]
        [StringLength(50)]
        public string Status { get; set; } = string.Empty;
    }
}
