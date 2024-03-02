using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace MealGeniusBackend.Services
{
    public class RabbitMQConsumerHostedService : IHostedService
    {
        private readonly RabbitMQService _rabbitMQService;
        private readonly ILogger<RabbitMQConsumerHostedService> _logger;

        public RabbitMQConsumerHostedService(RabbitMQService rabbitMQService, ILogger<RabbitMQConsumerHostedService> logger)
        {
            _rabbitMQService = rabbitMQService;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _rabbitMQService.ConsumeMessage();
                _logger.LogInformation("RabbitMQ Consumer has started consuming messages.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while starting the RabbitMQ message consumer.");
                // Consider handling the error appropriately
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("RabbitMQ Consumer Hosted Service stopping.");
            return Task.CompletedTask;
        }
    }
}
