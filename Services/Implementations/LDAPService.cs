using rapat_backend.Services.Interfaces;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;

namespace rapat_backend.Services.Implementations
{
    public class LdapService(IConfiguration configuration, ILogger<LdapService> logger) : ILdapService
    {
        private readonly string _ldapServer = configuration["Key:LDAPServer"]!;
        private readonly string _ldapDN = configuration["Key:LDAPDN"]!;
        private readonly ILogger<LdapService> _logger = logger;

        public async Task<(bool IsSuccess, string? NormalizedUsername, string? ErrorMessage)> AuthenticateAsync(string username, string password)
        {
            if (configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
            {
                _logger.LogInformation("LDAP Bypass Development mode aktif buat user: {User}", username);
                return (true, username, "");
            }

            return await Task.Run<(bool IsSuccess, string? NormalizedUsername, string? ErrorMessage)>(() =>
            {
                try
                {
                    using var connection = new LdapConnection(_ldapServer);
                    connection.AuthType = AuthType.Basic;
                    connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;

                    // 1. Cari data user dulu pake akun Manager buat dapet sAMAccountName asli
                    connection.Bind(new NetworkCredential($"polman\\{configuration["Key:LDAPSSOManagerUsername"]!}", configuration["Key:LDAPSSOManagerPassword"]!));

                    // Filter OR: cari di sAMAccountName (username), description, atau employeeID (biasanya tempat NPK)
                    string filter = $"(|(sAMAccountName={username})(description={username})(employeeID={username}))";
                    var searchRequest = new SearchRequest(
                        _ldapDN,
                        filter,
                        SearchScope.Subtree,
                        "sAMAccountName"
                    );

                    var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                    if (searchResponse.Entries.Count == 0)
                    {
                        _logger.LogWarning("User {User} kaga ketemu di LDAP pake filter {Filter}", username, filter);
                        return (false, null, "User tidak ditemukan.");
                    }

                    var entry = searchResponse.Entries[0];
                    var realUsername = entry.Attributes["sAMAccountName"][0].ToString()!;
                    _logger.LogInformation("LDAP User ketemu: {Input} -> {Real}", username, realUsername);

                    // 2. Coba login (bind) pake password yang diinput user
                    connection.Bind(new NetworkCredential($"polman\\{realUsername}", password));
                    _logger.LogInformation("LDAP Bind sukses buat {Real}", realUsername);

                    return (true, realUsername, "");
                }
                catch (LdapException ex)
                {
                    _logger.LogWarning(ex, "LDAP Auth gagal buat {User} (Code: {Code})", username, ex.ErrorCode);
                    return (false, null, "Username atau password salah.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fatal pas autentikasi LDAP buat {User}", username);
                    return (false, null, "Terjadi kesalahan pada server autentikasi.");
                }
            });
        }

        private async Task<string?> GetAttributeAsync(string samAccountName, string attributeName)
        {
            if (configuration["ASPNETCORE_ENVIRONMENT"] == "Development")
            {
                return samAccountName;
            }

            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new LdapConnection(_ldapServer);
                    connection.AuthType = AuthType.Basic;
                    connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
                    connection.Bind(new NetworkCredential($"polman\\{configuration["Key:LDAPSSOManagerUsername"]!}", configuration["Key:LDAPSSOManagerPassword"]!));

                    string filter = $"(|(sAMAccountName={samAccountName})(description={samAccountName})(employeeID={samAccountName}))";

                    var searchRequest = new SearchRequest(
                        _ldapDN,
                        filter,
                        SearchScope.Subtree,
                        attributeName
                    )
                    {
                        SizeLimit = 1
                    };

                    var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                    if (searchResponse.Entries.Count > 0)
                    {
                        var entry = searchResponse.Entries[0];
                        if (entry.Attributes.Contains(attributeName))
                        {
                            var attribute = entry.Attributes[attributeName];
                            return Encoding.UTF8.GetString((byte[])attribute.GetValues(typeof(byte[]))[0]);
                        }
                    }
                    return null;
                }
                catch (LdapException ex)
                {
                    _logger.LogError(ex, "Koneksi ke server LDAP gagal: {Error}", ex.Message);
                    return samAccountName;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gagal mendapatkan atribut {Attribute} untuk username {User}", attributeName, samAccountName);
                    return samAccountName;
                }
            });
        }

        public Task<string?> GetUsernameAsync(string samAccountName)
        {
            return GetAttributeAsync(samAccountName, "sAMAccountName");
        }

        public Task<string?> GetMailAsync(string samAccountName)
        {
            return GetAttributeAsync(samAccountName, "mail");
        }

        public Task<string?> GetDisplayNameAsync(string samAccountName)
        {
            return GetAttributeAsync(samAccountName, "displayName");
        }
    }
}
