using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;

namespace rapat_backend.Services.Interfaces
{
    public interface IAzureStorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
        Task<string> UploadFormFileAsync(IFormFile file, string prefixFolder);
    }
}
