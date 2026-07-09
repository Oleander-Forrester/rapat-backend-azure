namespace rapat_backend.Services.Interfaces
{
    public interface ILdapService
    {
        Task<(bool IsSuccess, string? NormalizedUsername, string? ErrorMessage)> AuthenticateAsync(string username, string password);
        Task<string?> GetUsernameAsync(string samAccountName);
        Task<string?> GetMailAsync(string samAccountName);
        Task<string?> GetDisplayNameAsync(string samAccountName);
    }
}
