using rapat_backend.Models;
using rapat_backend.Services.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;

namespace rapat_backend.Services.Implementations
{
    public class UserService(IConfiguration config) : IUserService
    {
        private readonly string _conn = config.GetConnectionString("LoginConnection")!;
        private readonly string _appConn = config.GetConnectionString("DefaultConnection")!;
        private const string UsernameParam = "@Username";
        private const string ApplicationParam = "@Aplikasi";
        private const string RoleParam = "@Role";

        public async Task<(bool IsSuccess, List<Aplikasi> ListAplikasi, string? ErrorMessage)> AuthenticateAsync(string username, string jenisAplikasi)
        {
            try
            {
                var list = new List<Aplikasi>();
                bool isUserExist = false;

                await using var conn = new SqlConnection(_conn);
                await using var cmd = new SqlCommand("sso_getAppByUsername", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue(UsernameParam, username);
                cmd.Parameters.AddWithValue("@JenisAplikasi", jenisAplikasi);

                await conn.OpenAsync();
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    isUserExist = true;
                    do
                    {
                        list.Add(new Aplikasi
                        {
                            NamaAplikasi = reader.GetString(reader.GetOrdinal("app_deskripsi")),
                            NamaRole = reader.GetString(reader.GetOrdinal("rol_deskripsi")),
                            Root = reader.GetString(reader.GetOrdinal("app_tautan")),
                            AppId = reader.GetString(reader.GetOrdinal("app_id")),
                            RoleId = reader.GetString(reader.GetOrdinal("rol_id")),
                            AppIcon = reader.GetString(reader.GetOrdinal("app_icon"))
                        });
                    } while (await reader.ReadAsync());
                }

                if (isUserExist)
                    return (true, list, "");
                return (false, list, "Username atau password tidak valid.");
            }
            catch (Exception ex)
            {
                return (false, [], $"Gagal mendapatkan daftar aplikasi: {ex.Message}");
            }
        }

        public async Task<(bool IsSuccess, List<Menu> ListMenu, string? ErrorMessage)> GetListMenuAsync(string username, string aplikasi, string role)
        {
            try
            {
                var list = new List<Menu>();

                await using var conn = new SqlConnection(_conn);
                await using var cmd = new SqlCommand("sso_getMenuByUsername", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue(UsernameParam, username);
                cmd.Parameters.AddWithValue(ApplicationParam, aplikasi);
                cmd.Parameters.AddWithValue(RoleParam, role);

                await conn.OpenAsync();
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    do
                    {
                        list.Add(new Menu
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("men_id")),
                            ParentId = reader.GetInt32(reader.GetOrdinal("men_parent_id")),
                            Icon = reader.GetString(reader.GetOrdinal("men_icon")),
                            Label = reader.GetString(reader.GetOrdinal("men_nama")),
                            Href = reader.GetString(reader.GetOrdinal("men_link"))
                        });
                    } while (await reader.ReadAsync());
                }

                var lookup = list.ToLookup(p => p.ParentId);
                foreach (var menu in list)
                {
                    menu.Children = [.. lookup[menu.Id]];
                }

                return (true, lookup[0].ToList(), "");
            }
            catch (Exception ex)
            {
                return (false, [], $"Gagal mendapatkan daftar menu: {ex.Message}");
            }
        }

        public async Task<(bool IsSuccess, List<string> ListPermission, string? ErrorMessage)> GetPermissionAsync(string username, string aplikasi, string role)
        {
            try
            {
                var list = new List<string>();

                await using var conn = new SqlConnection(_conn);
                await using var cmd = new SqlCommand("sso_getListAkses", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue(UsernameParam, username);
                cmd.Parameters.AddWithValue(ApplicationParam, aplikasi);
                cmd.Parameters.AddWithValue(RoleParam, role);

                await conn.OpenAsync();
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    do
                    {
                        list.Add(reader.GetString(reader.GetOrdinal("permission")));
                    } while (await reader.ReadAsync());
                }
                return (true, list, "");
            }
            catch (Exception ex)
            {
                return (false, [], $"Gagal mendapatkan daftar hak akses: {ex.Message}");
            }
        }

        public async Task<bool> HasPermissionAsync(string username, string aplikasi, string role, string permission)
        {
            try
            {
                await using var conn = new SqlConnection(_conn);
                await using var cmd = new SqlCommand("sso_getAksesByUsername", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue(UsernameParam, username);
                cmd.Parameters.AddWithValue(ApplicationParam, aplikasi);
                cmd.Parameters.AddWithValue(RoleParam, role);
                cmd.Parameters.AddWithValue("@Permission", permission);

                await conn.OpenAsync();
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(string? KaryawanId, string? FullName, string? PasswordHash)> GetEmployeeDetailsAsync(string username)
        {
            try
            {
                string? kryId = null;
                string? fullName = null;
                string? kryUsername = null;
                
                await using (var appConn = new SqlConnection(_appConn))
                {
                    string queryKaryawan = "SELECT kry_id, kry_nama_depan, kry_nama_blkg, kry_username FROM ess_mskaryawan WHERE kry_username = @Username OR kry_id = @Username";
                    await using var cmdKaryawan = new SqlCommand(queryKaryawan, appConn);
                    cmdKaryawan.Parameters.AddWithValue("@Username", username);

                    await appConn.OpenAsync();
                    await using var readerKaryawan = await cmdKaryawan.ExecuteReaderAsync();
                    if (await readerKaryawan.ReadAsync())
                    {
                        kryId = readerKaryawan.GetString(readerKaryawan.GetOrdinal("kry_id"));
                        string depan = readerKaryawan.IsDBNull(readerKaryawan.GetOrdinal("kry_nama_depan")) ? "" : readerKaryawan.GetString(readerKaryawan.GetOrdinal("kry_nama_depan"));
                        string blkg = readerKaryawan.IsDBNull(readerKaryawan.GetOrdinal("kry_nama_blkg")) ? "" : readerKaryawan.GetString(readerKaryawan.GetOrdinal("kry_nama_blkg"));
                        kryUsername = readerKaryawan.IsDBNull(readerKaryawan.GetOrdinal("kry_username")) ? "" : readerKaryawan.GetString(readerKaryawan.GetOrdinal("kry_username"));
                        
                        fullName = $"{depan} {blkg}".Trim();
                    }
                }

                if (kryId != null && kryUsername != null)
                {
                    await using var loginConn = new SqlConnection(_conn);
                    string queryUser = "SELECT usr_password FROM sso_msuser WHERE usr_id = @UsrId";
                    await using var cmdUser = new SqlCommand(queryUser, loginConn);
                    cmdUser.Parameters.AddWithValue("@UsrId", kryUsername);

                    await loginConn.OpenAsync();
                    var pwdObj = await cmdUser.ExecuteScalarAsync();
                    string? pwdHash = pwdObj != null && pwdObj != DBNull.Value ? pwdObj.ToString() : null;

                    return (kryId, fullName, pwdHash);
                }
                
                return (null, null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error GetEmployeeDetailsAsync: {ex.Message}");
                return (null, null, null);
            }
        }
    }
}
