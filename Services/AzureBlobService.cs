using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace MealGeniusBackend.Services;

public interface IAzureBlobService
{
    Task<string> UploadImageBytesAsync(byte[] imageBytes, string imageName);
}

public class AzureBlobService(IOptions<AzureStorageConfig> options, GenerationCancellation cancellation) : IAzureBlobService
{
    public async Task<string> UploadImageBytesAsync(byte[] imageBytes, string imageName)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ConnectionString))
            throw new InvalidOperationException("Missing AzureStorageConfig:ConnectionString.");
        using var input = new MemoryStream(imageBytes);
        using var image = await Image.LoadAsync(input, cancellation.Token);
        using var output = new MemoryStream();
        await image.SaveAsync(output, new JpegEncoder { Quality = 75 }, cancellation.Token);
        output.Position = 0;
        var container = new BlobServiceClient(options.Value.ConnectionString)
            .GetBlobContainerClient(options.Value.ContainerName);
        // Provision public read access explicitly outside the application; do not change an account's access policy here.
        var blob = container.GetBlobClient(imageName + ".jpg");
        await blob.UploadAsync(output, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" }
        }, cancellation.Token);
        return blob.Uri.ToString();
    }
}
public class AzureStorageConfig
{
    public string ConnectionString { get; set; } = "";
    public string ContainerName { get; set; } = "mealgenius-meals-images";
}
