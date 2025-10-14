using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImageOrchestrator
{
    public class ImageOrchestrator
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ILogger<ImageOrchestrator> _logger;

        public ImageOrchestrator(ILogger<ImageOrchestrator> logger)
        {
            _logger = logger;
            var connectionString = System.Environment.GetEnvironmentVariable("AzureServiceBusConnectionString");
            _serviceBusClient = new ServiceBusClient(connectionString);
        }

        [FunctionName("ImageOrchestrator")]
        public async Task Run(
            [ServiceBusTrigger("image-processing", Connection = "AzureServiceBusConnectionString")] string myQueueItem)
        {
            _logger.LogInformation("🎯 Orchestrator recebeu mensagem");

            try
            {
                var message = JsonConvert.DeserializeObject<ImageMessage>(myQueueItem);

                if (string.IsNullOrEmpty(message.FileName))
                {
                    _logger.LogError("❌ FileName está vazio");
                    return;
                }

                _logger.LogInformation("🔄 Distribuindo processamento para: {FileName}", message.FileName);

                // Publicar nas filas especializadas EM PARALELO
                var tasks = new List<Task>
                {
                    PublishToQueue("image-resize-small", message, new { Size = "small", Width = 200 }),
                    PublishToQueue("image-resize-medium", message, new { Size = "medium", Width = 500 }),
                    PublishToQueue("image-resize-large", message, new { Size = "large", Width = 800 })
                };

                await Task.WhenAll(tasks);
                _logger.LogInformation("✅ Orchestrator distribuiu processamento com sucesso!");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro no Orchestrator");
                throw;
            }
        }

        private async Task PublishToQueue(string queueName, ImageMessage originalMessage, object processingInfo)
        {
            try
            {
                var message = new
                {
                    OriginalMessage = originalMessage,
                    Processing = processingInfo,
                    OrchestratedAt = System.DateTime.UtcNow
                };

                var messageBody = JsonConvert.SerializeObject(message);
                var sender = _serviceBusClient.CreateSender(queueName);
                await sender.SendMessageAsync(new ServiceBusMessage(messageBody));

                _logger.LogInformation("📤 Publicado na fila {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao publicar na fila {QueueName}", queueName);
                throw;
            }
        }
    }

    public class ImageMessage
    {
        public string UserId { get; set; }
        public string ImageUrl { get; set; }
        public string FileName { get; set; }
        public System.DateTime UploadedAt { get; set; }
    }
}
