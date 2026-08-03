using Microsoft.Data.SqlClient;
using rapat_backend.Services.Interfaces;
using System.Data;
using System.Text;
using System.Text.Json;

namespace rapat_backend.Services.Implementations
{
    public class PushNotificationService(IConfiguration config, IHttpClientFactory httpClientFactory) : IPushNotificationService
    {
        private readonly string _conn = config.GetConnectionString("DefaultConnection")!;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task<bool> SavePushTokenAsync(string npk, string expoPushToken)
        {
            if (string.IsNullOrEmpty(npk) || string.IsNullOrEmpty(expoPushToken)) return false;

            try
            {
                await using var conn = new SqlConnection(_conn);
                var query = @"
                    IF EXISTS (SELECT 1 FROM Rapat_UserPushTokens WHERE Npk = @Npk)
                    BEGIN
                        UPDATE Rapat_UserPushTokens 
                        SET ExpoPushToken = @Token, LastUpdated = GETDATE() 
                        WHERE Npk = @Npk
                    END
                    ELSE
                    BEGIN
                        INSERT INTO Rapat_UserPushTokens (Npk, ExpoPushToken, LastUpdated)
                        VALUES (@Npk, @Token, GETDATE())
                    END";

                await using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Npk", npk);
                cmd.Parameters.AddWithValue("@Token", expoPushToken);

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error SavePushToken: {ex.Message}");
                return false;
            }
        }

        public async Task SendNotificationAsync(List<string> npkList, string title, string body, object? data = null)
        {
            if (npkList == null || !npkList.Any()) return;

            var tokens = new List<string>();

            try
            {
                await using var conn = new SqlConnection(_conn);
                var npkParams = string.Join(",", npkList.Select((_, i) => $"@npk{i}"));
                var query = $"SELECT ExpoPushToken FROM Rapat_UserPushTokens WHERE Npk IN ({npkParams})";

                await using var cmd = new SqlCommand(query, conn);
                for (int i = 0; i < npkList.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@npk{i}", npkList[i]);
                }

                await conn.OpenAsync();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    tokens.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error GetPushTokens: {ex.Message}");
                return;
            }

            if (!tokens.Any()) return;

            // Prepare Expo Notification payload
            var messages = tokens.Select(token => new
            {
                to = token,
                sound = "default",
                title = title,
                body = body,
                data = data
            });

            try
            {
                var client = _httpClientFactory.CreateClient();
                var json = JsonSerializer.Serialize(messages);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await client.PostAsync("https://exp.host/--/api/v2/push/send", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending push notification to Expo: {ex.Message}");
            }
        }
    }
}
