using rapat_backend.Models;

namespace rapat_backend.DTOs.Auth
{
    public class MenuResponseDto
    {
        public List<Menu> ListMenu { get; set; } = [];
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
