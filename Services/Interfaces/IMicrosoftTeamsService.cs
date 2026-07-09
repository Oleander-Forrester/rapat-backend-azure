using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace rapat_backend.Services.Interfaces
{
    public interface IMicrosoftTeamsService
    {
        Task<string> CreateTeamsMeetingAsync(
            string judul,
            DateTime mulai,
            DateTime selesai,
            List<string> emailPeserta,
            bool isOnline,
            string? locationName,
            string pesanTambahan = ""
        );


        Task<string> UpdateTeamsMeetingAsync(
            string eventId,    
            string judul,
            DateTime mulai,
            DateTime selesai,
            List<string> emailPeserta,
            bool isOnline,
            string? locationName,
            string pesanTambahan = ""
        );

        Task<bool> CancelTeamsMeetingAsync(string eventId, string judulAsli);
    }
}