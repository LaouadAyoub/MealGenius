using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Processing;
using Azure.Identity;

namespace MealGeniusBackend.Services
{
    public interface IAzureBlobService
    {
        Task<string> UploadImageAsync(string imageUrl, string containerName = "mealgenius-meals-images", string imageName = "");
        Task<string> UploadImageCompressedAsync(string imageUrl, string containerName = "mealgenius-meals-images", string imageName = "");
        Task<string> UploadCompressedImageToAzureAsync(Stream imageStream, string name);
        Task DeleteOriginalImageFromAzureAsync(string imageUrl);

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

        public async Task DeleteOriginalImageFromAzureAsync(string imageUrl)
        {
            var uri = new Uri(imageUrl);
            // Create a BlobServiceClient object which will be used to create a container client
            var blobServiceClient = new BlobServiceClient(_storageConfig.ConnectionString);

            // Extract the BlobContainerName and BlobName from the imageUrl
            var blobContainerName = uri.Segments[1].TrimEnd('/'); // "mealgenius-grocery-items-images"
            var blobName = string.Join("", uri.Segments, 2, uri.Segments.Length - 2); // "img-8EaRGxVKUowTBXGX3H1radDi.png"

            var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
            var blobClient = blobContainerClient.GetBlobClient(blobName);

            // Deletes the blob if it exists
            await blobClient.DeleteIfExistsAsync();
        }
        public async Task<string> UploadCompressedImageToAzureAsync(Stream imageStream, string name)
        {
            var blobServiceClient = new BlobServiceClient(_storageConfig.ConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient("compressed-images");
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var newFileName = $"{name}-compressed.jpg";
            var blobClient = containerClient.GetBlobClient(newFileName);

            await blobClient.UploadAsync(imageStream, new BlobHttpHeaders { ContentType = "image/jpeg" });
            return blobClient.Uri.ToString();
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

        public async Task<string> UploadImageCompressedAsync(string imageUrl, string containerName = "mealgenius-meals-images", string imageName = "")
        {
            try
            {
                _logger.LogInformation("Uploading an image to Azure Blob Storage.");

                HttpResponseMessage response = await _httpClient.GetAsync(imageUrl);
                if (response.IsSuccessStatusCode)
                {
                    using (Stream imageStream = await response.Content.ReadAsStreamAsync())
                    {
                        // Use ImageSharp to compress the image
                        using (var image = SixLabors.ImageSharp.Image.Load(imageStream))
                        {
                            // Resize the image if you want to change its dimensions
                            image.Mutate(x => x.Resize(512, 512));

                            // Choose an appropriate encoder for your image format, here we use JPEG
                            var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                            {
                                // Adjust the quality for compression (0 - 100)
                                Quality = 75
                            };

                            // Create a new memory stream to store the compressed image
                            using (var compressedStream = new MemoryStream())
                            {
                                // Save the image to the stream using the encoder
                                await image.SaveAsync(compressedStream, encoder);
                                compressedStream.Seek(0, SeekOrigin.Begin); // Reset stream position after saving

                                BlobServiceClient blobServiceClient = new BlobServiceClient(_storageConfig.ConnectionString);
                                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                                await containerClient.CreateIfNotExistsAsync();
                                await containerClient.SetAccessPolicyAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);

                                string fileName = Path.GetFileName(new Uri(imageUrl).AbsolutePath);
                                BlobClient blobClient = containerClient.GetBlobClient(fileName);

                                BlobHttpHeaders headers = new BlobHttpHeaders
                                {
                                    ContentType = "image/jpeg" // Ensure this matches the format you saved the image in
                                };

                                // Upload the compressed image stream to Azure
                                await blobClient.UploadAsync(compressedStream, new BlobUploadOptions
                                {
                                    HttpHeaders = headers,
                                    Metadata = new Dictionary<string, string>
                                {
                                    { "Name", imageName }
                                }
                                });

                                _logger.LogInformation("Image uploaded successfully to {BlobUri}", blobClient.Uri);

                                return blobClient.Uri.ToString();
                            }
                        }
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
