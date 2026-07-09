using rapat_backend.DTOs.Rapat;
using rapat_backend.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Linq;

namespace rapat_backend.Repositories.Implementations
{
    public class RapatRepository(IConfiguration config) : IRapatRepository
    {
        private readonly string _conn = config.GetConnectionString("DefaultConnection")!;

        public async Task<int> CreateAsync(CreateRapatRequest dto, string username)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_createRapatBaru", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@Judul", dto.Judul);
            cmd.Parameters.AddWithValue("@Jenis", dto.Jenis ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RuanganId", (dto.Mode == "Online" || dto.RuanganId <= 0) ? DBNull.Value : dto.RuanganId);
            cmd.Parameters.AddWithValue("@NamaRuanganManual", (object?)dto.NamaRuanganManual ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WaktuMulai", dto.WaktuMulai);
            cmd.Parameters.AddWithValue("@WaktuSelesai", dto.WaktuSelesai);
            cmd.Parameters.AddWithValue("@Mode", dto.Mode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Link", string.IsNullOrWhiteSpace(dto.Link) ? (object)DBNull.Value : dto.Link);
            cmd.Parameters.AddWithValue("@CreatedBy", username);

            var pesertaCSV = dto.PesertaKaryawanIds != null ? string.Join(",", dto.PesertaKaryawanIds) : "";
            cmd.Parameters.AddWithValue("@PesertaCSV", pesertaCSV);

            string stringExternal = "";
            if (dto.PesertaExternal != null && dto.PesertaExternal.Count > 0)
            {
                foreach (var p in dto.PesertaExternal)
                {
                    string cleanNama = p.Nama?.Replace("|", "").Replace(";", "") ?? "";
                    string cleanEmail = p.Email?.Replace("|", "").Replace(";", "") ?? "";
                    if (!string.IsNullOrWhiteSpace(cleanNama) && !string.IsNullOrWhiteSpace(cleanEmail))
                    {
                        stringExternal += $"{cleanNama}|{cleanEmail};";
                    }
                }
            }

            if (!string.IsNullOrEmpty(stringExternal))
                cmd.Parameters.AddWithValue("@PesertaExternalString", stringExternal);
            else
                cmd.Parameters.AddWithValue("@PesertaExternalString", DBNull.Value);

            await conn.OpenAsync();
            object? result = await cmd.ExecuteScalarAsync();
            return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
        }

        public async Task<bool> UpdateAsync(UpdateRapatRequest dto, string usernameEditor)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_updateRapat", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@RapatID", dto.RapatId);
            cmd.Parameters.AddWithValue("@Judul", dto.Judul);
            cmd.Parameters.AddWithValue("@Jenis", dto.Jenis ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@RuanganID", (dto.Mode == "Online" || dto.RuanganId <= 0) ? DBNull.Value : dto.RuanganId);
            cmd.Parameters.AddWithValue("@NamaRuanganManual", (object?)dto.NamaRuanganManual ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WaktuMulai", dto.WaktuMulai);
            cmd.Parameters.AddWithValue("@WaktuSelesai", dto.WaktuSelesai);
            cmd.Parameters.AddWithValue("@Mode", dto.Mode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Link", string.IsNullOrWhiteSpace(dto.Link) ? (object)DBNull.Value : dto.Link);
            cmd.Parameters.AddWithValue("@UsernameEditor", usernameEditor);

            var pesertaCSV = dto.PesertaKaryawanIds != null ? string.Join(",", dto.PesertaKaryawanIds) : "";
            cmd.Parameters.AddWithValue("@PesertaCSV", pesertaCSV);

            string stringExternal = "";
            if (dto.PesertaExternal != null && dto.PesertaExternal.Count > 0)
            {
                foreach (var p in dto.PesertaExternal)
                {
                    string cleanNama = p.Nama?.Replace("|", "").Replace(";", "") ?? "";
                    string cleanEmail = p.Email?.Replace("|", "").Replace(";", "") ?? "";
                    if (!string.IsNullOrWhiteSpace(cleanNama) && !string.IsNullOrWhiteSpace(cleanEmail))
                    {
                        stringExternal += $"{cleanNama}|{cleanEmail};";
                    }
                }
            }

            if (!string.IsNullOrEmpty(stringExternal))
                cmd.Parameters.AddWithValue("@PesertaExternalString", stringExternal);
            else
                cmd.Parameters.AddWithValue("@PesertaExternalString", DBNull.Value);

            await conn.OpenAsync();

            await cmd.ExecuteNonQueryAsync();

            return true;
        }

        public async Task<bool> UpdateStatusAsync(int rapatId, string status, string usernameEditor, string? link = null)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_updateStatusRapat", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@RapatID", rapatId);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@UsernameEditor", usernameEditor);
            cmd.Parameters.AddWithValue("@LinkMeeting", string.IsNullOrWhiteSpace(link) ? (object)DBNull.Value : link);

            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> CancelAsync(int id, string username)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_cancelRapat", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@RapatID", id);
            cmd.Parameters.AddWithValue("@UsernameEditor", username);

            var outputParam = new SqlParameter("@RowsAffected", SqlDbType.Int) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(outputParam);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(outputParam.Value) > 0;
        }

        public async Task<bool> UpdateAbsensiAsync(int rapatId, string? karyawanId, string? email, string statusHadir, string? keterangan, string? peranBaru, string usernameEditor)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_getDataAbsensiRapat", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@RapatID", rapatId);
            cmd.Parameters.AddWithValue("@KaryawanID", string.IsNullOrEmpty(karyawanId) ? DBNull.Value : karyawanId);
            cmd.Parameters.AddWithValue("@Email", string.IsNullOrEmpty(email) ? DBNull.Value : email);
            cmd.Parameters.AddWithValue("@StatusHadir", statusHadir);
            cmd.Parameters.AddWithValue("@Keterangan", (object?)keterangan ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PeranBaru", (object?)peranBaru ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UsernameEditor", usernameEditor);

            var outputParam = new SqlParameter("@RowsAffected", SqlDbType.Int) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(outputParam);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(outputParam.Value) > 0;
        }

        public async Task<string> GetStatusKehadiranUserAsync(int rapatId, string username)
        {
            string status = "-";

            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_getAbsensiUser", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@RapatID", rapatId);
            cmd.Parameters.AddWithValue("@Username", username);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();

            if (result != null && result != DBNull.Value)
            {
                status = result.ToString() ?? "-";
            }

            return string.IsNullOrWhiteSpace(status) ? "-" : status;
        }


        public async Task<bool> CreateMoMAsync(CreateMoMRequest dto, string filePath, string usernameEditor)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_createMoMRapat", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@RapatID", dto.RapatId);
            cmd.Parameters.AddWithValue("@IsiNotulensi", (object?)dto.IsiNotulensi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FileDokumentasi", string.IsNullOrWhiteSpace(filePath) ? DBNull.Value : (object)filePath);
            cmd.Parameters.AddWithValue("@UsernameEditor", usernameEditor);

            var outputParam = new SqlParameter("@RowsAffected", SqlDbType.Int) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(outputParam);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(outputParam.Value) > 0;
        }

        public async Task<bool> DeleteItemAksiAsync(int id)
        {
            using (var conn = new SqlConnection(_conn))
            {
                using (var cmd = new SqlCommand("ars_deleteItemAksi", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@ItemAksiId", id);

                    await conn.OpenAsync();
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    return rowsAffected > 0;
                }
            }
        }

        public async Task<bool> CreateItemAksiAsync(CreateItemAksiRequest dto, string usernamePembuat)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_createItemAksi", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@RapatID", dto.RapatId);
            cmd.Parameters.AddWithValue("@Deskripsi", dto.Deskripsi);
            cmd.Parameters.AddWithValue("@Deadline", dto.Deadline);
            cmd.Parameters.AddWithValue("@UsernamePembuat", usernamePembuat);
            cmd.Parameters.AddWithValue("@KaryawanIDDitugaskan", dto.KaryawanIdDitugaskan);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<bool> UpdateStatusItemAksiAsync(int tindakLanjutId, string statusBaru, string? filePath, string username)
        {
            using (var conn = new SqlConnection(_conn))
            {
                await conn.OpenAsync();

                using (var cmd = new SqlCommand("ars_editStatusItemAksi", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@TindakLanjutID", tindakLanjutId);
                    cmd.Parameters.AddWithValue("@StatusBaru", statusBaru);

                    cmd.Parameters.AddWithValue("@FileDokumentasi", (object?)filePath ?? DBNull.Value);

                    cmd.Parameters.AddWithValue("@UsernameEditor", username);

                    var result = await cmd.ExecuteScalarAsync();

                    return (result != null && Convert.ToInt32(result) > 0);
                }
            }
        }

        public async Task<IEnumerable<string>> GetStatusListAsync()
        {
            var list = new List<string>();
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_getDataRapatStatus", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { list.Add(reader[0].ToString() ?? ""); }
            return list;
        }

        public async Task<IEnumerable<KaryawanDto>> GetListKaryawanAsync()
        {
            var list = new List<KaryawanDto>();
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_getDataListKaryawan", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new KaryawanDto
                {
                    KaryawanId = reader["KaryawanId"].ToString() ?? "",
                    NamaKaryawan = reader["NamaKaryawan"].ToString() ?? "",
                    Email = reader["Email"].ToString() ?? "",
                    Jabatan = reader["Jabatan"].ToString() ?? ""
                });
            }
            return list;
        }

        public async Task<IEnumerable<RuanganDto>> GetListRuanganAsync()
        {
            var list = new List<RuanganDto>();
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_getDataListRuangan", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RuanganDto
                {
                    RuanganId = reader.GetInt32(reader.GetOrdinal("RuanganId")),
                    NamaRuangan = reader["NamaRuangan"].ToString() ?? ""
                });
            }
            return list;
        }

        public async Task<(IEnumerable<RapatDto> Data, int TotalData)> GetAllByUserAsync(
         string username,
         int page,
         int pageSize,
         string search,
         string status,
         string sort,
         string? jenis,
         DateTime? startDate = null,
         DateTime? endDate = null)
        {
            var list = new List<RapatDto>();
            int totalData = 0;

            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_getDataRapatByUser", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@Username", username);
            cmd.Parameters.AddWithValue("@PageNumber", page);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);
            cmd.Parameters.AddWithValue("@SearchKeyword", search ?? "");
            cmd.Parameters.AddWithValue("@Status", status ?? "");
            cmd.Parameters.AddWithValue("@Sort", sort ?? "rap_waktu_mulai desc");
            cmd.Parameters.AddWithValue("@Jenis", jenis ?? "");
            cmd.Parameters.Add("@StartDate", SqlDbType.DateTime).Value = (object?)startDate ?? DBNull.Value;
            cmd.Parameters.Add("@EndDate", SqlDbType.DateTime).Value = (object?)endDate ?? DBNull.Value;

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (totalData == 0 && HasColumn(reader, "TotalData"))
                {
                    totalData = reader.GetInt32(reader.GetOrdinal("TotalData"));
                }

                list.Add(new RapatDto
                {
                    Id = reader.GetInt32(reader.GetOrdinal("rap_id")),
                    Judul = reader.GetString(reader.GetOrdinal("rap_judul_rapat")),
                    Jenis = reader.IsDBNull(reader.GetOrdinal("rap_jenis")) ? "-" : reader.GetString(reader.GetOrdinal("rap_jenis")),
                    RuanganNama = reader.IsDBNull(reader.GetOrdinal("rua_nama"))
                ? (reader.IsDBNull(reader.GetOrdinal("rap_lokasi_manual")) ? "Online" : reader.GetString(reader.GetOrdinal("rap_lokasi_manual")))
                : reader.GetString(reader.GetOrdinal("rua_nama")),
                    WaktuMulai = reader.GetDateTime(reader.GetOrdinal("rap_waktu_mulai")),
                    WaktuSelesai = reader.GetDateTime(reader.GetOrdinal("rap_waktu_selesai")),
                    Mode = reader.IsDBNull(reader.GetOrdinal("rap_mode")) ? "" : reader.GetString(reader.GetOrdinal("rap_mode")),
                    Status = reader.GetString(reader.GetOrdinal("rap_status")),
                    PembuatNama = reader.IsDBNull(reader.GetOrdinal("pembuat_nama")) ? "" : reader.GetString(reader.GetOrdinal("pembuat_nama")),
                    PembuatUsername = reader.IsDBNull(reader.GetOrdinal("rap_created_by")) ? "" : reader.GetString(reader.GetOrdinal("rap_created_by")),
                    LinkMeeting = HasColumn(reader, "rap_link_meeting") && !reader.IsDBNull(reader.GetOrdinal("rap_link_meeting"))
                 ? reader.GetString(reader.GetOrdinal("rap_link_meeting"))
                 : null
                });
            }

            return (list, totalData);
        }

        public async Task<RapatDetailDto?> GetDetailRapatAsync(int rapatId)
        {
            return await GetByIdAsync(rapatId);
        }

        public async Task UpdateEventIdAsync(int rapatId, string eventId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_updateEventIdRapat", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@RapatID", rapatId);
            cmd.Parameters.AddWithValue("@EventID", eventId);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<string>> GetEmailsByKaryawanIdsAsync(List<string> karyawanIds)
        {
            var emails = new List<string>();
            if (karyawanIds == null || !karyawanIds.Any()) return emails;

            string csvIds = string.Join(",", karyawanIds);

            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_getEmailsByKaryawanList", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@KaryawanIDs", csvIds);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader["Email"] != DBNull.Value)
                {
                    emails.Add(reader["Email"].ToString() ?? "");
                }
            }
            return emails;
        }

        public async Task<string> GetRuanganNameById(int ruanganId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_getNamaRuangan", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@RuanganID", ruanganId);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "Ruangan Tidak Ditemukan";
        }

        public async Task<RapatDetailDto?> GetByIdAsync(int rapatId, string? username = null)
        {
            RapatDetailDto? rapat = null;
            using (var conn = new SqlConnection(_conn))
            {
                await conn.OpenAsync();

                using (var cmd = new SqlCommand("ars_getDataRapatDetail", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@RapatID", rapatId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            rapat = new RapatDetailDto
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("rap_id")),
                                Judul = reader.GetString(reader.GetOrdinal("rap_judul_rapat")),

                                EventId = HasColumn(reader, "rap_event_id") && reader["rap_event_id"] != DBNull.Value
                               ? reader["rap_event_id"].ToString()
                               : null,

                                Jenis = reader["rap_jenis"] != DBNull.Value ? (reader["rap_jenis"].ToString() ?? "-") : "-",
                                RuanganNama = reader["rua_nama"] != DBNull.Value ? (reader["rua_nama"].ToString() ?? "Online") : (reader["rap_lokasi_manual"] != DBNull.Value ? (reader["rap_lokasi_manual"].ToString() ?? "Online") : "Online"),
                                RuanganId = reader["rua_id"] != DBNull.Value ? Convert.ToInt32(reader["rua_id"]) : 0,
                                NamaRuanganManual = reader["rap_lokasi_manual"] != DBNull.Value ? (reader["rap_lokasi_manual"].ToString() ?? "") : "",
                                WaktuMulai = reader.GetDateTime(reader.GetOrdinal("rap_waktu_mulai")),
                                WaktuSelesai = reader.GetDateTime(reader.GetOrdinal("rap_waktu_selesai")),
                                Mode = reader["rap_mode"].ToString() ?? "",
                                Status = reader["rap_status"].ToString() ?? "",
                                LinkMeeting = reader["rap_link_meeting"] != DBNull.Value ? reader["rap_link_meeting"].ToString() : null,
                                IsiNotulensi = reader["rap_isi_notulensi"] != DBNull.Value ? reader["rap_isi_notulensi"].ToString() : null,
                                FileDokumentasi = reader["rap_file_dokumentasi"] != DBNull.Value ? reader["rap_file_dokumentasi"].ToString() : null,
                                PembuatUsername = reader["rap_created_by"] != DBNull.Value ? (reader["rap_created_by"].ToString() ?? "") : "",
                                PembuatNama = reader["pembuat_nama"] != DBNull.Value
                                 ? (reader["pembuat_nama"].ToString() ?? "-")
                                 : (reader["rap_created_by"] != DBNull.Value ? (reader["rap_created_by"].ToString() ?? "-") : "-"),

                                Peserta = new List<PesertaRapatDetailDto>(),
                                ItemAksi = new List<ItemAksiRapatDetailDto>()
                            };
                        }
                    }
                }

                if (rapat == null) return null;

                // Wrap peserta fetch in try-catch so main rapat data is still returned
                try
                {
                    using (var cmdP = new SqlCommand("ars_getDataPesertaRapat", conn))
                    {
                        cmdP.CommandType = CommandType.StoredProcedure;
                        cmdP.Parameters.AddWithValue("@RapatID", rapatId);
                        using (var rP = await cmdP.ExecuteReaderAsync())
                        {
                            while (await rP.ReadAsync())
                            {
                                rapat.Peserta.Add(new PesertaRapatDetailDto
                                {
                                    KaryawanId = rP["kry_id"] != DBNull.Value ? rP["kry_id"].ToString() : "",
                                    Nama = rP["nama"].ToString() ?? "",
                                    Username = rP["username"]?.ToString() ?? "",
                                    Jabatan = rP["jab_main_id"] != DBNull.Value ? rP["jab_main_id"].ToString() : "-",
                                    StatusHadir = rP["pra_hadir"].ToString() ?? "",
                                    Peran = rP["pra_peran"].ToString() ?? "",
                                    IsNotulis = (rP["pra_peran"].ToString() ?? "").ToLower() == "notulis",
                                    Keterangan = rP["pra_keterangan"] != DBNull.Value ? rP["pra_keterangan"].ToString() : "",
                                    IsExternal = !rP.IsDBNull(rP.GetOrdinal("is_external")) && rP.GetInt32(rP.GetOrdinal("is_external")) == 1,
                                    Email = HasColumn(rP, "email") ? (rP["email"] != DBNull.Value ? rP["email"].ToString() : "") : ""
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING GetByIdAsync] Gagal ambil peserta rapat {rapatId}: {ex.Message}");
                }

                var normalizedLogin = (username ?? "").Trim();
                var currentPeserta = rapat.Peserta.FirstOrDefault(p =>
                  !string.IsNullOrWhiteSpace(p.Username) &&
                  !string.IsNullOrWhiteSpace(normalizedLogin) &&
                  string.Equals(p.Username.Trim(), normalizedLogin, StringComparison.OrdinalIgnoreCase));

                var currentKey = "";
                var currentKeyType = "none";
                var currentIsExternal = false;
                if (currentPeserta != null)
                {
                    currentIsExternal = currentPeserta.IsExternal;
                    if (currentPeserta.IsExternal)
                    {
                        currentKey = (currentPeserta.Email ?? "").Trim().ToLowerInvariant();
                        currentKeyType = string.IsNullOrWhiteSpace(currentKey) ? "none" : "email";
                    }
                    else
                    {
                        currentKey = (currentPeserta.KaryawanId ?? "").Trim();
                        currentKeyType = string.IsNullOrWhiteSpace(currentKey) ? "none" : "karyawanId";
                    }
                }

                // Wrap item aksi fetch in try-catch so main rapat data is still returned
                try
                {
                    using (var cmdA = new SqlCommand("ars_getDataItemAksiRapat", conn))
                    {
                        cmdA.CommandType = CommandType.StoredProcedure;
                        cmdA.Parameters.AddWithValue("@RapatID", rapatId);
                        cmdA.Parameters.AddWithValue("@UsernameLogin", (object?)username ?? DBNull.Value);

                        using (var rA = await cmdA.ExecuteReaderAsync())
                        {
                            while (await rA.ReadAsync())
                            {
                                var itemAksi = new ItemAksiRapatDetailDto
                                {
                                    Id = Convert.ToInt32(rA["Id"]),
                                    Deskripsi = rA["Deskripsi"].ToString() ?? "",
                                    PIC_KaryawanId = rA["PIC_KaryawanId"] != DBNull.Value ? (rA["PIC_KaryawanId"].ToString() ?? "") : "",
                                    FileBukti = rA["FileBukti"]?.ToString(),
                                    PIC_Nama = rA["PIC_Nama"] != DBNull.Value ? (rA["PIC_Nama"].ToString() ?? "PIC Tidak Ditemukan") : "PIC Tidak Ditemukan",
                                    Deadline = Convert.ToDateTime(rA["Deadline"]),
                                    Status = rA["Status"].ToString() ?? "",
                                    ModifDate = rA["ModifDate"] != DBNull.Value ? Convert.ToDateTime(rA["ModifDate"]) : null,

                                    IsCurrentUserPIC = rA["IsCurrentUserPIC"] != DBNull.Value && Convert.ToInt32(rA["IsCurrentUserPIC"]) == 1
                                };

                                bool computedIsPic = false;
                                var picRaw = (itemAksi.PIC_KaryawanId ?? "").Trim();
                                if (!string.IsNullOrWhiteSpace(currentKey) && !string.IsNullOrWhiteSpace(picRaw))
                                {
                                    if (currentKeyType == "email")
                                    {
                                        computedIsPic = string.Equals(picRaw.ToLowerInvariant(), currentKey, StringComparison.OrdinalIgnoreCase);
                                    }
                                    else if (currentKeyType == "karyawanId")
                                    {
                                        computedIsPic = string.Equals(picRaw, currentKey, StringComparison.Ordinal);
                                    }
                                }
                                itemAksi.IsCurrentUserPIC = itemAksi.IsCurrentUserPIC || computedIsPic;

                                rapat.ItemAksi.Add(itemAksi);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING GetByIdAsync] Gagal ambil item aksi rapat {rapatId}: {ex.Message}");
                }
            }
            return rapat;
        }

        public async Task<bool> CreateJenisRapatAsync(string namaJenis, string status = "Aktif")
        {
            using (var conn = new SqlConnection(_conn))
            {
                using (var cmd = new SqlCommand("ars_createJenisRapat", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@NamaJenis", namaJenis);
                    cmd.Parameters.AddWithValue("@Status", status ?? "Aktif");

                    try
                    {
                        await conn.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                    catch (SqlException ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
            }
        }

        public async Task<bool> UpdateJenisRapatAsync(int id, string namaJenis, string status = "Aktif")
        {
            using (var conn = new SqlConnection(_conn))
            {
                using (var cmd = new SqlCommand("ars_updateJenisRapat", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@JenisID", id);
                    cmd.Parameters.AddWithValue("@NamaJenis", namaJenis);
                    cmd.Parameters.AddWithValue("@Status", status ?? "Aktif");

                    try
                    {
                        await conn.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                    catch (SqlException ex)
                    {
                        // Tangkap error dari RAISERROR di SQL
                        throw new Exception(ex.Message);
                    }
                }
            }
        }

        public async Task<bool> DeleteJenisRapatAsync(int id)
        {
            using (var conn = new SqlConnection(_conn))
            {
                using (var cmd = new SqlCommand("ars_deleteJenisRapat", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@JenisID", id);

                    try
                    {
                        await conn.OpenAsync();
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                    catch (SqlException ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
            }
        }

        public async Task<bool> ToggleStatusJenisRapatAsync(int id)
        {
            using (var conn = new SqlConnection(_conn))
            {
                using (var cmd = new SqlCommand("ars_toggleStatusJenisRapat", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@JenisID", id);

                    try
                    {
                        await conn.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                    catch (SqlException ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
            }
        }

        public async Task<IEnumerable<dynamic>> GetListJenisRapatAsync()
        {
            var list = new List<dynamic>();

            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("ars_getListJenisRapat", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new
                {
                    value = Convert.ToInt32(reader["Value"]),
                    text = reader["Text"].ToString(),
                    status = HasColumn(reader, "Status") ? reader["Status"].ToString() : "Aktif"
                });
            }
            return list;
        }

        private bool HasColumn(SqlDataReader r, string columnName)
        {
            try { return r.GetOrdinal(columnName) >= 0; }
            catch (IndexOutOfRangeException) { return false; }
        }
    }
}
