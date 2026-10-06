using System.Text;
using MealGeniusBackend.Models;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace MealGeniusBackend.Services.RabbitMQ;

public sealed class RabbitMQService(IConfiguration configuration) : IDisposable
{
    public const string Queue = "mealgenius.generation.v2";
    public const string FailedQueue = "mealgenius.generation.failed";
    private readonly object publishLock = new();
    private IConnection? connection;
    private IModel? publisher;

    public IConnection Connect()
    {
        if (!configuration.GetValue("Messaging:Enabled", true))
            throw new InvalidOperationException("Messaging is disabled.");
        var factory = new ConnectionFactory
        {
            HostName = configuration["RABBITMQ_HOSTNAME"] ?? "localhost",
            Port = configuration.GetValue("RABBITMQ_PORT", 5672),
            UserName = configuration["RABBITMQ_USERNAME"] ?? throw new InvalidOperationException("Missing RABBITMQ_USERNAME."),
            Password = configuration["RABBITMQ_PASSWORD"] ?? throw new InvalidOperationException("Missing RABBITMQ_PASSWORD."),
            DispatchConsumersAsync = true, AutomaticRecoveryEnabled = true,
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };
        if (configuration.GetValue("RABBITMQ_TLS", false))
            factory.Ssl = new SslOption { Enabled = true, ServerName = factory.HostName };
        return factory.CreateConnection("MealGenius");
    }

    public static void Declare(IModel channel)
    {
        channel.QueueDeclare(FailedQueue, true, false, false);
        channel.QueueDeclare(Queue, true, false, false, new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = "",
            ["x-dead-letter-routing-key"] = FailedQueue
        });
    }

    public void Publish(UserTaskDTO task)
    {
        if (task.Id == Guid.Empty || string.IsNullOrWhiteSpace(task.UserId))
            throw new ArgumentException("A task ID and user ID are required.");
        lock (publishLock)
        {
            if (publisher?.IsOpen != true)
            {
                publisher?.Dispose();
                connection?.Dispose();
                connection = Connect();
                publisher = connection.CreateModel();
                Declare(publisher);
                publisher.ConfirmSelect();
            }
            var properties = publisher.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.MessageId = task.Id.ToString();
            publisher.BasicPublish("", Queue, properties, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(task)));
            publisher.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
        }
    }

    public void Dispose()
    {
        lock (publishLock) { publisher?.Dispose(); connection?.Dispose(); }
    }
}
