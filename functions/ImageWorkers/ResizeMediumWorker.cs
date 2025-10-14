using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ImageWorkers
{
    public class ResizeMediumWorker
    {
        private readonly ILogger<ResizeMediumWorker> _logger;

        public ResizeMediumWorker(ILogger<ResizeMediumWorker> logger)
        {
            _logger = logger;
        }

        [FunctionName("ResizeMediumWorker")]
        public async Task Run(
            [ServiceBusTrigger("image-resize-medium", Connection = "AzureServiceBusConnectionString")] string myQueueItem)
        {
            _logger.LogInformation("🟠 [MediumWorker] Iniciando processamento MEDIUM");

            try
            {
                var message = JsonConvert.DeserializeObject<WorkerMessage>(myQueueItem);
                await ProcessImage(message, "medium", 500);

                _logger.LogInformation("✅ [MediumWorker] Processamento MEDIUM concluído: {FileName}",
                    message.OriginalMessage.FileName);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "❌ [MediumWorker] Erro no processamento");
                throw;
            }
        }

        private async Task ProcessImage(WorkerMessage message, string sizeName, int targetSize)
        {
            var storageConnectionString = System.Environment.GetEnvironmentVariable("AzureStorageConnectionString");
            var blobServiceClient = new BlobServiceClient(storageConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient("photos");

            // Download da imagem original
            var originalBlobClient = containerClient.GetBlobClient(message.OriginalMessage.FileName);

            if (!await originalBlobClient.ExistsAsync())
            {
                _logger.LogError("❌ [MediumWorker] Blob não encontrado: {FileName}", message.OriginalMessage.FileName);
                return;
            }

            using var originalStream = new MemoryStream();
            await originalBlobClient.DownloadToAsync(originalStream);
            originalStream.Position = 0;

            // Processar imagem
            using var image = await Image.LoadAsync(originalStream);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(targetSize, targetSize),
                Mode = ResizeMode.Max
            }));

            // Salvar versão redimensionada
            var resizedFileName = GetResizedFileName(message.OriginalMessage.FileName, sizeName);
            var resizedBlobClient = containerClient.GetBlobClient(resizedFileName);

            using var outputStream = new MemoryStream();
            await image.SaveAsync(outputStream, new JpegEncoder());
            outputStream.Position = 0;

            await resizedBlobClient.UploadAsync(outputStream, overwrite: true);

            _logger.LogInformation("📁 [MediumWorker] {Size} criado: {FileName}",
                sizeName.ToUpper(), resizedFileName);
        }

        private static string GetResizedFileName(string originalFileName, string size)
        {
            var directory = Path.GetDirectoryName(originalFileName);
            var fileName = Path.GetFileNameWithoutExtension(originalFileName);
            var extension = Path.GetExtension(originalFileName);
            return $"{directory}/{size}/{fileName}{extension}";
        }
    }
}