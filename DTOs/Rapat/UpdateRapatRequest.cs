using System.ComponentModel.DataAnnotations;

namespace rapat_backend.DTOs.Rapat
{
    public class UpdateRapatRequest : CreateRapatRequest
    {
        [Required]
        public int RapatId { get; set; }
        public string? Status { get; set; }
    }
}
