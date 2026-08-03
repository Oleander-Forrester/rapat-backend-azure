using Microsoft.Data.SqlClient;
using rapat_backend.DTOs.Auth;
using System.Data;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace rapat_backend.Services.Interfaces
{
    public interface IPushNotificationService
    {
        Task<bool> SavePushTokenAsync(string npk, string expoPushToken);
        Task SendNotificationAsync(List<string> npkList, string title, string body, object? data = null);
    }
}
