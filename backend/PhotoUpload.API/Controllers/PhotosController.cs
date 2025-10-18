using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Messaging.ServiceBus;

namespace PhotoUpload.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhotosController : ControllerBase
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly IConfiguration _configuration;

        public PhotosController(IConfiguration configuration)
        {
            _configuration = configuration;

            // Configuração do Blob Storage
            var storageConnectionString = _configuration["AzureStorageConnectionString"];
            _blobServiceClient = new BlobServiceClient(storageConnectionString);

            // Configuração do Service Bus
            var serviceBusConnectionString = _configuration["AzureServiceBusConnectionString"];
            _serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            // Validar tipo de arquivo
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest("Tipo de arquivo não permitido.");

            try
            {
                var userId = "user123"; // Em produção, pegar do JWT
                var fileName = $"{userId}/{Guid.NewGuid()}{fileExtension}";

                // Upload para Blob Storage
                var containerClient = _blobServiceClient.GetBlobContainerClient("photos");
                await containerClient.CreateIfNotExistsAsync();

                var blobClient = containerClient.GetBlobClient(fileName);
                await blobClient.UploadAsync(file.OpenReadStream(), overwrite: true);

                // Publicar mensagem no Service Bus
                var message = new
                {
                    UserId = userId,
                    ImageUrl = blobClient.Uri.ToString(),
                    FileName = fileName,
                    UploadedAt = DateTime.UtcNow
                };

                var messageBody = JsonSerializer.Serialize(message);
                var sender = _serviceBusClient.CreateSender("image-processing");
                await sender.SendMessageAsync(new ServiceBusMessage(Encoding.UTF8.GetBytes(messageBody)));

                return Ok(new
                {
                    Message = "Foto enviada com sucesso!",
                    ImageUrl = blobClient.Uri.ToString(),
                    FileName = fileName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro interno: {ex.Message}");
            }
        }

        [HttpGet("my-photos")]
        public async Task<IActionResult> GetMyPhotos()
        {
            var userId = "user123";
            var blobs = new List<string>();

            var containerClient = _blobServiceClient.GetBlobContainerClient("photos");

            // REMOVER FILTRO temporariamente
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: userId))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                blobs.Add(blobClient.Uri.ToString());
            }

            return Ok(blobs);
        }        
    }
}
