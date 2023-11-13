using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading.Tasks;
using static MealGeniusBackend.Controllers.MainAPIController;

namespace MealGeniusBackend.Services
{

    public class RabbitMQService : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RabbitMQService(IServiceScopeFactory serviceScopeFactory)
        {
            var factory = new ConnectionFactory()
            {
                HostName = "host.docker.internal",
                UserName = "root",
                Password = "root"
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            // Further queue declaration and other setup here
            _channel.QueueDeclare(queue: "task_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

            _serviceScopeFactory = serviceScopeFactory;
        }

        public void PublishMessageInTaskQueue(string message)
        {
            var body = Encoding.UTF8.GetBytes(message);

            _channel.BasicPublish(exchange: "", routingKey: "REDACTED", basicProperties: null, body: body);
        }

        public void ConsumeMessage()
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                UserTaskDTO userTaskDTO = JsonConvert.DeserializeObject<UserTaskDTO>(message);

                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var mealPlanService = scope.ServiceProvider.GetRequiredService<IMealPlanService>();
                    var userDashboardService = scope.ServiceProvider.GetRequiredService<IUserDashboardService>();

                    userDashboardService.GenerateUserDashboard(userTaskDTO);
                    await mealPlanService.GenerateMealPlan(userTaskDTO);
                }

                Console.WriteLine($"Received: {message}");
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
