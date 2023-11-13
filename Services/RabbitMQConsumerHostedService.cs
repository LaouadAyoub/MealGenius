using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace MealGeniusBackend.Services
{
    public class RabbitMQConsumerHostedService : IHostedService
    {
        private readonly RabbitMQService _rabbitMQService;

        public RabbitMQConsumerHostedService(RabbitMQService rabbitMQService)
        {
            _rabbitMQService = rabbitMQService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _rabbitMQService.ConsumeMessage();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
