using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading.Tasks;
using MealGeniusBackend.Models;
using MealGeniusBackend.Services.Dashboard;
using MealGeniusBackend.DataAccess;
using MealGeniusBackend.DataAcess;

namespace MealGeniusBackend.Services.RabbitMQ
{

    public class RabbitMQService : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RabbitMQService(IServiceScopeFactory serviceScopeFactory, ILogger<RabbitMQService> logger)
        {
            _logger = logger;

            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost",
                    Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out int port) ? port : 5672,
                    UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "root",
                    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "root"
                };
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                // Further queue declaration and other setup here
                _channel.QueueDeclare(queue: "task_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

                _serviceScopeFactory = serviceScopeFactory;

                _logger.LogInformation("RabbitMQ Service has been initialized.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing RabbitMQ Service.");
                throw; // Rethrow if you need to notify callers
            }
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
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        _logger.LogInformation($"Received message: {message}");

                        UserTaskDTO userTaskDTO = JsonConvert.DeserializeObject<UserTaskDTO>(message);
                        // Change the status of the task to processing
                        var userTask = dbContext.Tasks.Find(userTaskDTO.Id);
                        userTask.Status = UserTaskStatus.Ongoing;
                        dbContext.Tasks.Update(userTask);
                        dbContext.SaveChanges();

                        var mealPlanService = scope.ServiceProvider.GetRequiredService<IMealPlanService>();
                        var userDashboardService = scope.ServiceProvider.GetRequiredService<IUserDashboardService>();
                        var groceryListService = scope.ServiceProvider.GetRequiredService<IGroceryListService>();
                        var mealsImagesService = scope.ServiceProvider.GetRequiredService<IMealsImagesService>();

                        // Generate the dashboard
                        var timer = new ServiceTaskTimer("RabbitMQService", "Start the generation");
                        timer.Start();
                        //await userDashboardService.GenerateUserDashboard(userTaskDTO);
                        //await mealPlanService.GenerateMealPlan(userTaskDTO);
                        //await Task.WhenAll(
                        //    groceryListService.GenerateGroceryList(userTaskDTO),
                        //    mealsImagesService.GenerateMealsImages(userTaskDTO)
                        //);
                        timer.StopAndLog();

                        #region temporary code for testing
                        // temporary for testing
                        string dashboardJson = Path.Combine(Directory.GetCurrentDirectory(), "JsonFiles", "Dashboard.json");
                        string groceryListJson = Path.Combine(Directory.GetCurrentDirectory(), "JsonFiles", "groceryList.json");
                        string meals_no_imagesJson = Path.Combine(Directory.GetCurrentDirectory(), "JsonFiles", "Mealplan_no_images.json");
                        string mealsWithImagesJson = Path.Combine(Directory.GetCurrentDirectory(), "JsonFiles", "Mealplan_Images.json");

                        // verify all the paths
                        
                        if (! ((System.IO.File.Exists(dashboardJson)) && (File.Exists(groceryListJson)) && (File.Exists(meals_no_imagesJson)) && (File.Exists(mealsWithImagesJson))))
                        {
                            throw new FileNotFoundException($" The file does not exist. {dashboardJson} {groceryListJson} {meals_no_imagesJson} {mealsWithImagesJson}");
                        }
                        var dashboard = System.IO.File.ReadAllText(dashboardJson);
                        var groceryList = System.IO.File.ReadAllText(groceryListJson);
                        var meals_no_images = System.IO.File.ReadAllText(meals_no_imagesJson);
                        var mealsWithImages = System.IO.File.ReadAllText(mealsWithImagesJson);

                        var username = "MoroccanCuisineLover";
                        var exampleUser = dbContext.Users.FirstOrDefault(u => u.UserName == username);
                        var exampleDashboard = dbContext.UserDashboards.FirstOrDefault(ud => ud.User == exampleUser);


                        // wait 1 minute
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        dbContext.UserDashboards.Add(new UserDashboard
                        {
                            UserId = userTaskDTO.UserId,
                            UserDashboardVersion = 1,
                            MicroGuide = exampleDashboard.MicroGuide,
                            MacroTargets = exampleDashboard.MacroTargets,
                            UserGoalsGuide = exampleDashboard.UserGoalsGuide,
                            WaterIntake = exampleDashboard.WaterIntake,
                            JsonUserKeyInfos = exampleDashboard.JsonUserKeyInfos,
                            TaskId = userTaskDTO.Id
                        });
                        dbContext.SaveChanges();
                        _logger.LogInformation("Task track : UserDashboard added");
                        // wait 1 minute
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        var mealplan = new MealPlan
                        {
                            UserId = userTaskDTO.UserId,
                            MealPlanVersion = 1,
                            GroceryListVersion = 0,
                            MealsImagesVersion = 0,
                            MealPlanJson = meals_no_images,
                            GroceryListJson = "",
                            TaskId = userTaskDTO.Id,
                            Title = "Meal Plan",
                        };
                        dbContext.MealPlans.Add(mealplan);
                        dbContext.SaveChanges();
                        _logger.LogInformation("Task track : MealPlan added without images");
                        // wait 1 minute
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        mealplan.MealPlanJson = mealsWithImages;
                        mealplan.MealsImagesVersion = 1;
                        dbContext.SaveChanges();
                        _logger.LogInformation("Task track : MealPlan added with images");
                        // wait 1 minute
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        mealplan.GroceryListJson = groceryList;
                        mealplan.GroceryListVersion = 1;
                        dbContext.SaveChanges();
                        _logger.LogInformation("Task track : GroceryList added");
                        #endregion

                        _logger.LogInformation($"Processed message successfully: {message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "RabbitMQ Service : Error processing message.");
                        // Consider handling the failure such as re-queuing the message or notifying an admin
                    }
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
