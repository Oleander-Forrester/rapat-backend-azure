using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

public class BlobStorageService
{
    private readonly string _connectionString;
    private readonly string _containerName;

    public BlobStorageService(IConfiguration config)
    {
        _connectionString = config["Azure:Storage:ConnectionString"]!;
        _containerName = config["Azure:Storage:ContainerName"]!;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folder)
    {
        var blobServiceClient = new BlobServiceClient(_connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"{folder}/{Guid.NewGuid()}{fileExtension}";
        var blobClient = containerClient.GetBlobClient(fileName);

        using (var stream = file.OpenReadStream())
        {
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
        }

        return blobClient.Uri.ToString();
    }

    public async Task<string> UploadFileAsync(string localFilePath, string contentType, string folder)
    {
        var blobServiceClient = new BlobServiceClient(_connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var fileExtension = Path.GetExtension(localFilePath);
        var fileName = $"{folder}/{Guid.NewGuid()}{fileExtension}";
        var blobClient = containerClient.GetBlobClient(fileName);

        using (var stream = File.OpenRead(localFilePath))
        {
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType });
        }

        return blobClient.Uri.ToString();
    }
}
