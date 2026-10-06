using System.Text;
using MealGeniusBackend.Models;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MealGeniusBackend.Services.RabbitMQ;

public class RabbitMQConsumerHostedService(RabbitMQService broker, IServiceScopeFactory scopes,
    ILogger<RabbitMQConsumerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        using var connection = broker.Connect();
        using var channel = connection.CreateModel();
        RabbitMQService.Declare(channel);
        channel.BasicQos(0, 1, false);
        var gate = new SemaphoreSlim(1, 1);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, delivery) =>
        {
            await gate.WaitAsync();
            try
            {
                stoppingToken.ThrowIfCancellationRequested();
                var task = JsonConvert.DeserializeObject<UserTaskDTO>(Encoding.UTF8.GetString(delivery.Body.ToArray()));
                if (task is null || task.Id == Guid.Empty || string.IsNullOrWhiteSpace(task.UserId))
                    throw new InvalidDataException("Invalid generation message.");
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<GenerationJobProcessor>().Process(task, stoppingToken);
                channel.BasicAck(delivery.DeliveryTag, false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                if (channel.IsOpen) channel.BasicNack(delivery.DeliveryTag, false, true);
            }
            catch (Exception ex)
            {
                logger.LogError("Generation delivery failed ({ErrorType}); routing to failed queue.", ex.GetType().Name);
                if (channel.IsOpen) channel.BasicNack(delivery.DeliveryTag, false, false);
            }
            finally { gate.Release(); }
        };
        var consumerTag = channel.BasicConsume(RabbitMQService.Queue, false, consumer);
        logger.LogInformation("Generation consumer started with prefetch 1.");
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        // Cancel new deliveries and let the active callback finish cancellation before disposing the channel.
        await gate.WaitAsync();
        try { if (channel.IsOpen) channel.BasicCancel(consumerTag); }
        finally { gate.Release(); }
    }
}
