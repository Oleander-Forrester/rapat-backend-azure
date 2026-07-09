using rapat_backend.DTOs.Auth;

namespace rapat_backend.Services.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto dto);
        Task<PermissionResponseDto?> GetPermissionAsync(PermissionRequestDto dto);
        Task<MenuResponseDto?> GetMenuAsync(PermissionRequestDto dto);
    }
}
