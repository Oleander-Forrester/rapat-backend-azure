using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using rapat_backend.Services.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace rapat_backend.Services
{
    public class AzureStorageService : IAzureStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;

        public AzureStorageService(IConfiguration configuration)
        {
            var connectionString = configuration["Azure:Storage:ConnectionString"];
            _containerName = configuration["Azure:Storage:ContainerName"] ?? "rapat-uploads";
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException("Azure:Storage:ConnectionString is missing in appsettings.json");
            }
            
            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        private async Task<BlobContainerClient> GetContainerClientAsync()
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
            return containerClient;
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            var containerClient = await GetContainerClientAsync();
            var blobClient = containerClient.GetBlobClient(fileName);

            fileStream.Position = 0; // Ensure stream is at the beginning
            await blobClient.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType });

            return blobClient.Uri.ToString();
        }

        public async Task<string> UploadFormFileAsync(IFormFile file, string prefixFolder)
        {
            var extension = Path.GetExtension(file.FileName);
            var timestamp = DateTime.Now.ToString("yyMMddHHmmss");
            var uniqueFileName = $"{prefixFolder}/{Guid.NewGuid()}_{timestamp}{extension}";

            using var stream = file.OpenReadStream();
            return await UploadFileAsync(stream, uniqueFileName, file.ContentType);
        }
    }
}
