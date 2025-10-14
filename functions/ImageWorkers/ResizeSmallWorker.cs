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
    public class ResizeSmallWorker
    {
        private readonly ILogger<ResizeSmallWorker> _logger;

        public ResizeSmallWorker(ILogger<ResizeSmallWorker> logger)
        {
            _logger = logger;
        }

        [FunctionName("ResizeSmallWorker")]
        public async Task Run(
            [ServiceBusTrigger("image-resize-small", Connection = "AzureServiceBusConnectionString")] string myQueueItem)
        {
            _logger.LogInformation("🟡 [SmallWorker] Iniciando processamento SMALL");

            try
            {
                var message = JsonConvert.DeserializeObject<WorkerMessage>(myQueueItem);
                await ProcessImage(message, "small", 200);

                _logger.LogInformation("✅ [SmallWorker] Processamento SMALL concluído: {FileName}",
                    message.OriginalMessage.FileName);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "❌ [SmallWorker] Erro no processamento");
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
                _logger.LogError("❌ [SmallWorker] Blob não encontrado: {FileName}", message.OriginalMessage.FileName);
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

            _logger.LogInformation("📁 [SmallWorker] {Size} criado: {FileName}",
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

    public class WorkerMessage
    {
        public ImageMessage OriginalMessage { get; set; }
        public dynamic Processing { get; set; }
        public System.DateTime OrchestratedAt { get; set; }
    }

    public class ImageMessage
    {
        public string UserId { get; set; }
        public string ImageUrl { get; set; }
        public string FileName { get; set; }
        public System.DateTime UploadedAt { get; set; }
    }
}