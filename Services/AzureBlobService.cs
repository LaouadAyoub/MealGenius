using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace MealGeniusBackend.Services
{
    public interface IAzureBlobService
    {
        Task<string> UploadImageAsync(string imageUrl, string containerName = "mealgenius-meals-images", string imageName = "");
    }

    public class AzureBlobService : IAzureBlobService
    {
        private readonly AzureStorageConfig _storageConfig;
        private readonly ILogger<AzureBlobService> _logger;
        private readonly HttpClient _httpClient;
        public AzureBlobService(IOptions<AzureStorageConfig> storageConfig, ILogger<AzureBlobService> logger, HttpClient httpClient)
        {
            _storageConfig = storageConfig.Value;
            _logger = logger;
            _httpClient = httpClient;
        }
        public async Task<string> UploadImageAsync(string imageUrl, string containerName = "mealgenius-meals-images", string imageName = "")
        {
            try
            {
                _logger.LogInformation("Uploading an image to Azure Blob Storage.");

                HttpResponseMessage response = await _httpClient.GetAsync(imageUrl);
                if (response.IsSuccessStatusCode)
                {
                    using (Stream imageStream = await response.Content.ReadAsStreamAsync())
                    {
                        BlobServiceClient blobServiceClient = new BlobServiceClient(_storageConfig.ConnectionString);
                        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                        await containerClient.CreateIfNotExistsAsync();
                        await containerClient.SetAccessPolicyAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);

                        string fileName = Path.GetFileName(new Uri(imageUrl).AbsolutePath);
                        BlobClient blobClient = containerClient.GetBlobClient(fileName);

                        BlobHttpHeaders headers = new BlobHttpHeaders
                        {
                            ContentType = "image/png"
                        };
                        // Création des options de téléchargement pour inclure les métadonnées
                        BlobUploadOptions uploadOptions = new BlobUploadOptions
                        {
                            HttpHeaders = headers,
                            Metadata = new Dictionary<string, string>
                            {
                                { "Name", imageName }
                            }
                        };

                        await blobClient.UploadAsync(imageStream, uploadOptions);

                        _logger.LogInformation("Image uploaded successfully to {BlobUri}", blobClient.Uri);

                        return blobClient.Uri.ToString();
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to retrieve the image from the URL: {ImageUrl}", imageUrl);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image to Azure Blob Storage.");
                throw;
            }
        }
    }

    public class AzureStorageConfig
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; }
    }
}
