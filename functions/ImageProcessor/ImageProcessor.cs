using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.Azure.Functions.Worker;

namespace ImageProcessor
{
    public class ImageProcessorFunction
    {
        private readonly ILogger<ImageProcessorFunction> _logger;

        public ImageProcessorFunction(ILogger<ImageProcessorFunction> logger)
        {
            _logger = logger;
        }

        [Function("ImageProcessor")]
        public async Task Run(
            [ServiceBusTrigger("image-processing", Connection = "AzureServiceBusConnectionString")] string myQueueItem)
        {
            _logger.LogInformation("Processando mensagem: {Message}", myQueueItem);

            try
            {
                var message = JsonConvert.DeserializeObject<ImageMessage>(myQueueItem);

                if (string.IsNullOrEmpty(message.FileName))
                {
                    _logger.LogError("FileName está vazio na mensagem");
                    return;
                }

                var storageConnectionString = Environment.GetEnvironmentVariable("AzureStorageConnectionString");
                var blobServiceClient = new BlobServiceClient(storageConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient("photos");

                // Download da imagem original
                var originalBlobClient = containerClient.GetBlobClient(message.FileName);

                if (!await originalBlobClient.ExistsAsync())
                {
                    _logger.LogError("Blob não encontrado: {FileName}", message.FileName);
                    return;
                }

                using var originalStream = new MemoryStream();
                await originalBlobClient.DownloadToAsync(originalStream);
                originalStream.Position = 0;

                // Processar diferentes tamanhos
                var sizes = new[] {
                    new { Name = "small", Size = 200 },
                    new { Name = "medium", Size = 500 },
                    new { Name = "large", Size = 800 }
                };

                foreach (var size in sizes)
                {
                    originalStream.Position = 0;
                    using var image = await Image.LoadAsync(originalStream);

                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(size.Size, size.Size),
                        Mode = ResizeMode.Max
                    }));

                    var resizedFileName = GetResizedFileName(message.FileName, size.Name);
                    var resizedBlobClient = containerClient.GetBlobClient(resizedFileName);

                    using var outputStream = new MemoryStream();
                    await image.SaveAsync(outputStream, new JpegEncoder());
                    outputStream.Position = 0;

                    await resizedBlobClient.UploadAsync(outputStream, overwrite: true);
                    _logger.LogInformation("Imagem redimensionada criada: {FileName}", resizedFileName);
                }

                _logger.LogInformation("Processamento de imagem concluído com sucesso!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no processamento: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        private static string GetResizedFileName(string originalFileName, string size)
        {
            var directory = Path.GetDirectoryName(originalFileName);
            var fileName = Path.GetFileNameWithoutExtension(originalFileName);
            var extension = Path.GetExtension(originalFileName);
            return $"{directory}/{size}/{fileName}{extension}";
        }
    }

    public class ImageMessage
    {
        public string UserId { get; set; }
        public string ImageUrl { get; set; }
        public string FileName { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
