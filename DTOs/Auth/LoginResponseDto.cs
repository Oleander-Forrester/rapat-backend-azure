using rapat_backend.Models;

namespace rapat_backend.DTOs.Auth
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Nama { get; set; } = string.Empty;
        public string Npk { get; set; } = string.Empty;
        public List<Aplikasi> ListAplikasi { get; set; } = [];
        public DateTime ExpiresAt { get; set; } = default!;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
