using rapat_backend.Services.Interfaces;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace rapat_backend.Services.Implementations
{
    public class MicrosoftTeamsService : IMicrosoftTeamsService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MicrosoftTeamsService> _logger;

        private const string TimeZoneId = "SE Asia Standard Time";

        public MicrosoftTeamsService(IConfiguration config, ILogger<MicrosoftTeamsService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private GraphServiceClient GetGraphClient()
        {
            var tenantId = _config["AzureAd:TenantId"];
            var clientId = _config["AzureAd:ClientId"];
            var clientSecret = _config["AzureAd:ClientSecret"];

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("Konfigurasi AzureAd tidak lengkap di appsettings.json.");
            }

            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            return new GraphServiceClient(credential, scopes);
        }

        public async Task<string> CreateTeamsMeetingAsync(
            string judul,
            DateTime mulai,
            DateTime selesai,
            List<string> emailPeserta,
            bool isOnline,
            string? locationName,
            string pesanTambahan = "")
        {
            try
            {
                var graphClient = GetGraphClient();
                var userId = _config["AzureAd:OrganizerUserId"];

                if (string.IsNullOrEmpty(userId)) throw new InvalidOperationException("OrganizerUserId tidak dikonfigurasi.");

                var validEmails = emailPeserta?
                    .Where(email => !string.IsNullOrWhiteSpace(email) && email.Contains("@"))
                    .Distinct().ToList() ?? new List<string>();

                var startString = mulai.ToString("s");
                var endString = selesai.ToString("s");

                var attendees = validEmails.Select(email => new Attendee
                {
                    EmailAddress = new EmailAddress { Address = email },
                    Type = AttendeeType.Required
                }).ToList();

                var meetingTypeDesc = isOnline ? "Online (Microsoft Teams)" : $"Offline (Lokasi: {locationName ?? "-"})";

                string htmlBody = $@"
                <div style='font-family: Segoe UI, Arial, sans-serif; color: #333;'>
                    <h2 style='color: #0078D4;'>Undangan Rapat: {judul}</h2>
                    <div style='background-color: #f3f2f1; padding: 15px; border-radius: 8px;'>
                        <p><b>Topik:</b> {judul}</p>
                        <p><b>Waktu:</b> {mulai:dddd, dd MMMM yyyy}, {mulai:HH:mm} - {selesai:HH:mm} WIB</p>
                        <p><b>Lokasi:</b> {meetingTypeDesc}</p>
                    </div>
                    <p><b>Catatan:</b> {pesanTambahan}</p>
                </div>";

                var calendarEvent = new Event
                {
                    Subject = judul ?? "Rapat",
                    Start = new DateTimeTimeZone { DateTime = startString, TimeZone = TimeZoneId },
                    End = new DateTimeTimeZone { DateTime = endString, TimeZone = TimeZoneId },
                    Attendees = attendees,
                    Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
                    TransactionId = Guid.NewGuid().ToString()
                };

                if (isOnline)
                {
                    calendarEvent.IsOnlineMeeting = true;
                    calendarEvent.OnlineMeetingProvider = OnlineMeetingProviderType.TeamsForBusiness;
                }
                else
                {
                    calendarEvent.Location = new Location { DisplayName = locationName ?? "Lokasi Fisik" };
                }

                var createdEvent = await graphClient.Users[userId].Events
                    .Request()
                    .Header("Prefer", "outlook.timezone=\"SE Asia Standard Time\"")
                    .AddAsync(calendarEvent);

                string eventId = createdEvent.Id;
                string joinUrl = "";

                if (isOnline)
                {
                    if (createdEvent.OnlineMeeting != null && !string.IsNullOrEmpty(createdEvent.OnlineMeeting.JoinUrl))
                    {
                        joinUrl = createdEvent.OnlineMeeting.JoinUrl;
                    }
                    else if (!string.IsNullOrEmpty(createdEvent.OnlineMeetingUrl) && createdEvent.OnlineMeetingUrl.Contains("teams.microsoft.com", StringComparison.OrdinalIgnoreCase))
                    {
                        joinUrl = createdEvent.OnlineMeetingUrl;
                    }

                    int[] delaysMs = { 3000, 5000, 8000 };
                    foreach (var delayMs in delaysMs)
                    {
                        if (!string.IsNullOrEmpty(joinUrl)) break;

                        await Task.Delay(delayMs);

                        var refreshedEvent = await graphClient.Users[userId].Events[eventId]
                            .Request()
                            .Select("id,onlineMeeting,onlineMeetingUrl")
                            .GetAsync();

                        if (refreshedEvent.OnlineMeeting != null && !string.IsNullOrEmpty(refreshedEvent.OnlineMeeting.JoinUrl))
                        {
                            joinUrl = refreshedEvent.OnlineMeeting.JoinUrl;
                            break;
                        }
                        if (!string.IsNullOrEmpty(refreshedEvent.OnlineMeetingUrl) && refreshedEvent.OnlineMeetingUrl.Contains("teams.microsoft.com", StringComparison.OrdinalIgnoreCase))
                        {
                            joinUrl = refreshedEvent.OnlineMeetingUrl;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(joinUrl))
                    {
                        try
                        {
                            var onlineMeeting = new OnlineMeeting
                            {
                                StartDateTime = mulai,
                                EndDateTime = selesai,
                                Subject = judul ?? "Rapat",
                                Participants = new MeetingParticipants
                                {
                                    Attendees = validEmails.Select(email => new MeetingParticipantInfo
                                    {
                                        Upn = email
                                    }).ToList()
                                }
                            };

                            var createdOnlineMeeting = await graphClient.Users[userId].OnlineMeetings
                                .Request()
                                .AddAsync(onlineMeeting);

                            if (!string.IsNullOrEmpty(createdOnlineMeeting.JoinWebUrl))
                            {
                                joinUrl = createdOnlineMeeting.JoinWebUrl;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Gagal create online meeting via Online Meeting API sebagai fallback");
                        }
                    }
                }

                return $"{eventId}|{joinUrl}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal membuat event Teams: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<string> UpdateTeamsMeetingAsync(
            string eventId,
            string judul,
            DateTime mulai,
            DateTime selesai,
            List<string> emailPeserta,
            bool isOnline,
            string? locationName,
            string pesanTambahan = "")
        {
            try
            {
                if (string.IsNullOrEmpty(eventId)) return "";

                var graphClient = GetGraphClient();
                var userId = _config["AzureAd:OrganizerUserId"];

                var startString = mulai.ToString("s");
                var endString = selesai.ToString("s");

                var validEmails = emailPeserta?
                    .Where(email => !string.IsNullOrWhiteSpace(email) && email.Contains("@"))
                    .Distinct().ToList() ?? new List<string>();

                var attendees = validEmails.Select(email => new Attendee
                {
                    EmailAddress = new EmailAddress { Address = email },
                    Type = AttendeeType.Required
                }).ToList();

                var eventUpdate = new Event
                {
                    Subject = judul + " (Rescheduled)",
                    Start = new DateTimeTimeZone { DateTime = startString, TimeZone = TimeZoneId },
                    End = new DateTimeTimeZone { DateTime = endString, TimeZone = TimeZoneId },
                    Attendees = attendees
                };

                await graphClient.Users[userId].Events[eventId]
                    .Request()
                    .UpdateAsync(eventUpdate);

                return "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal update event Teams: {Message}", ex.Message);
                return "";
            }
        }

        public async Task<bool> CancelTeamsMeetingAsync(string eventId, string judulAsli)
        {
            try
            {
                if (string.IsNullOrEmpty(eventId)) return false;

                var graphClient = GetGraphClient();
                var userId = _config["AzureAd:OrganizerUserId"];

                var eventUpdate = new Event
                {
                    Subject = $"[DIBATALKAN] {judulAsli}",
                    ShowAs = FreeBusyStatus.Free,
                    Body = new ItemBody
                    {
                        ContentType = BodyType.Html,
                        Content = $"<h2 style='color:red'>RAPAT DIBATALKAN</h2><p>Rapat '{judulAsli}' telah dibatalkan.</p>"
                    }
                };

                await graphClient.Users[userId].Events[eventId]
                    .Request()
                    .UpdateAsync(eventUpdate);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal cancel event Teams: {Message}", ex.Message);
                return false;
            }
        }
    }
}