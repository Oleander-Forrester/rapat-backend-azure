using rapat_backend.DTOs.Rapat;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace rapat_backend.Repositories.Interfaces
{
    public interface IRapatRepository
    {
        Task<int> CreateAsync(CreateRapatRequest dto, string usernamePembuat);
        Task<bool> UpdateAsync(UpdateRapatRequest dto, string usernameEditor);
        Task<bool> UpdateStatusAsync(int rapatId, string status, string usernameEditor, string? link = null);
        Task<bool> CancelAsync(int rapatId, string usernameEditor);

        Task<bool> CreateMoMAsync(CreateMoMRequest dto, string filePath, string usernameEditor);

        Task<bool> UpdateAbsensiAsync(
            int rapatId,
            string? karyawanId,
            string? email,
            string statusHadir,
            string? keterangan,
            string? peranBaru,
            string usernameEditor);

        Task<bool> CreateItemAksiAsync(CreateItemAksiRequest dto, string usernamePembuat);

        Task<IEnumerable<KaryawanDto>> GetListKaryawanAsync();

        Task<IEnumerable<RuanganDto>> GetListRuanganAsync();

        Task<(IEnumerable<RapatDto> Data, int TotalData)> GetAllByUserAsync(
            string username,
            int page,
            int pageSize,
            string search,
            string status,
            string sort,
            string jenis,
            DateTime? startDate = null, 
            DateTime? endDate = null
        );

        Task<RapatDetailDto?> GetByIdAsync(int rapatId, string? username = null);

        Task<IEnumerable<string>> GetStatusListAsync();
        Task<string> GetStatusKehadiranUserAsync(int rapatId, string username);

        Task<RapatDetailDto?> GetDetailRapatAsync(int rapatId);
        Task<bool> UpdateStatusItemAksiAsync(int id, string status, string? filePath, string username);
        Task UpdateEventIdAsync(int rapatId, string eventId);
        Task<List<string>> GetEmailsByKaryawanIdsAsync(List<string> karyawanIds);
        Task<string> GetRuanganNameById(int ruanganId);
        Task<IEnumerable<dynamic>> GetListJenisRapatAsync();
        Task<bool> DeleteItemAksiAsync(int id);
        Task<bool> CreateJenisRapatAsync(string namaJenis, string status = "Aktif");
        Task<bool> UpdateJenisRapatAsync(int id, string namaJenis, string status = "Aktif");
        Task<bool> DeleteJenisRapatAsync(int id);
        Task<bool> ToggleStatusJenisRapatAsync(int id);
    }
}