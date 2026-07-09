using System.ComponentModel.DataAnnotations;

namespace rapat_backend.DTOs.Rapat
{
    public class PesertaExternalRequest
    {
        [Required(ErrorMessage = "Nama peserta external harus diisi.")]
        public string Nama { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email peserta external harus diisi.")]
        [EmailAddress(ErrorMessage = "Format email tidak valid.")]
        public string Email { get; set; } = string.Empty;
    }
}
