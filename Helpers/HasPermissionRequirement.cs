using Microsoft.AspNetCore.Authorization;

namespace rapat_backend.Helpers
{
    public class HasPermissionRequirement(string permission) : IAuthorizationRequirement
    {
        public string Permission { get; } = permission;
    }
}
