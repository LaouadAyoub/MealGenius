using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading.Tasks;
using MealGeniusBackend.Models;

namespace MealGeniusBackend.Services
{

    public class RabbitMQService : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger; 
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RabbitMQService(IServiceScopeFactory serviceScopeFactory, ILogger<RabbitMQService> logger)
        {
            var factory = new ConnectionFactory()
            {
                //HostName = "host.docker.internal",
                HostName = "localhost",
                Port = 5672,
                UserName = "root",
                Password = "root"
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            // Further queue declaration and other setup here
            _channel.QueueDeclare(queue: "task_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;

            _logger.LogInformation("RabbitMQ Service has been initialized.");
        }

        public void PublishMessageInTaskQueue(string message)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message);
                
                _channel.BasicPublish(exchange: "", routingKey: "REDACTED", basicProperties: null, body: body);
                _logger.LogInformation($"Message published to task_queue: {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to task_queue.");
                throw; // Rethrow if you need to notify callers
            }
        }

        public void ConsumeMessage()
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    _logger.LogInformation($"Received message: {message}");

                    UserTaskDTO userTaskDTO = JsonConvert.DeserializeObject<UserTaskDTO>(message);

                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var mealPlanService = scope.ServiceProvider.GetRequiredService<IMealPlanService>();
                        var userDashboardService = scope.ServiceProvider.GetRequiredService<IUserDashboardService>();
                        var groceryListService = scope.ServiceProvider.GetRequiredService<IGroceryListService>();
                        var mealsImagesService = scope.ServiceProvider.GetRequiredService<IMealsImagesService>();
                        await userDashboardService.GenerateUserDashboard(userTaskDTO);
                        await mealPlanService.GenerateMealPlan(userTaskDTO);

                        await Task.WhenAll(
                            groceryListService.GenerateGroceryList(userTaskDTO),
                            mealsImagesService.GenerateMealsImages(userTaskDTO)
                        );


                        _logger.LogInformation($"Processed message successfully: {message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message.");
                    // Consider handling the failure such as re-queuing the message or notifying an admin
                }
            };

            _channel.BasicConsume(queue: "task_queue",
                                  autoAck: true,
                                  consumer: consumer);
        }

        public void Dispose()
        {
            _channel.Close();
            _connection.Close();
        }
    }

}
