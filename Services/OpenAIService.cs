using MealGeniusBackend.Models.ErrorsHandlers;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Images;
using System.Text.RegularExpressions;

namespace MealGeniusBackend.Services
{
    public interface IOpenAIService
    {
        Task<string> GetResponseAsync(string systemPrompt, string userPrompt, OpenAI_API.Models.Model model, int tokens);
        Task<string> GenerateJsonBasedOnPromptResponseAsync(string systemPromptJson, string userPromptJson, int maxTokens, string model = "gpt-3.5-turbo-1106", double temperature = 0.0);
        Task<string> GenerateImageForMealAsync(string chatImagePromptResponse);

        Task<string> GenerateImageForMealAsyncFlexibleDelay(string chatImagePromptResponse);

    }

    public partial class OpenAIService : IOpenAIService
    {
        private readonly OpenAIAPI _openAiApi;
        private readonly ILogger<OpenAIService> _logger;  // Add this line

        public OpenAIService(OpenAIAPI openAiApi, ILogger<OpenAIService> logger)
        {
            _openAiApi = openAiApi;
            _logger = logger;  // Assign logger

        }

        public async Task<string> GetResponseAsync(string systemPrompt, string userPrompt, OpenAI_API.Models.Model model, int tokens)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var chat = CreateConversation(model, tokens);
                chat.AppendSystemMessage(systemPrompt);
                chat.AppendUserInput(userPrompt);
                var response = await chat.GetResponseFromChatbotAsync();
                return response;
            });
        }
        public async Task<string> GenerateJsonBasedOnPromptResponseAsync(string systemPromptJson, string userPromptJson, int maxTokens, string model = "gpt-3.5-turbo-1106" , double temperature = 0.0)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                _logger.LogInformation("GenerateJsonBasedOnPromptResponseAsync: Starting generation of JSON based on prompt response");
                var chatRequest = new ChatRequest()
                {
                    //Model = "gpt-3.5-turbo-1106", // Ajustez le modèle au besoin
                    //Model = "gpt-4-1106-preview",
                    Model = model,
                    Temperature = temperature,
                    MaxTokens = maxTokens,
                    ResponseFormat = ChatRequest.ResponseFormats.JsonObject,
                    Messages = new List<ChatMessage> {
                        new ChatMessage(ChatMessageRole.System, systemPromptJson),
                        new ChatMessage(ChatMessageRole.User, userPromptJson)
                    }
                };

                var chat = await _openAiApi.Chat.CreateChatCompletionAsync(chatRequest);
                var choice = chat.Choices.FirstOrDefault();
                var result = choice?.ToString();
                // Handle null result here if necessary
                if (result == null)
                {
                    throw new InvalidOperationException("The chat response did not include a choice.");
                }

                return result;
            }, maxRetries : 10);
        }


        public async Task<string> GenerateImageForMealAsync(string chatImagePromptResponse)
        {
            return await ExecuteImageOperationWithRetryAsync(async () =>
            {
                string imageUrl = string.Empty;

                var imageResponse = await _openAiApi.ImageGenerations.CreateImageAsync(
                new ImageGenerationRequest(chatImagePromptResponse, OpenAI_API.Models.Model.DALLE3, ImageSize._1024, "standard"));

                imageUrl = imageResponse.Data[0].Url.ToString();
                if (string.IsNullOrEmpty(imageUrl))
                {
                    throw new InvalidOperationException("The image generation response did not include a URL.");
                }

                _logger.LogInformation($"Image successfully generated for meal");
                return imageUrl; // Return the URL of the generated image
            }, maxRetries : 10);

        }

        public async Task<T> ExecuteImageOperationWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 5, double limitPerMinute = 7)
        {
            int attemptCount = 0;
            int initialDelayInSeconds = 5; // Initial delay
            int delayInSeconds = 20; // Délai initial

            while (true)
            {
                try
                {
                    attemptCount++;
                    return await operation();
                }
                catch (Exception ex)
                {
                    string errorContent = ex.Message; // Assurez-vous de récupérer le contenu de l'erreur correctement

                    int jsonStartIndex = errorContent.IndexOf("Content: {");
                    if (jsonStartIndex != -1)
                    {
                        // Extract the JSON substring from the message
                        string jsonContent = errorContent.Substring(jsonStartIndex + "Content: ".Length).Trim();
                        var errorResponse = JsonConvert.DeserializeObject<OpenApiErrorResponse>(jsonContent);
                        if (errorResponse?.Error?.Code == "rate_limit_exceeded")
                        {
                            _logger.LogError($"Tentative {attemptCount}: Limite de taux dépassée: {errorResponse.Error.Message}");

                            if (attemptCount >= maxRetries)
                            {
                                throw new Exception($"Impossible de compléter l'opération après {maxRetries} tentatives en raison de la limite de taux.", ex);
                            }

                            var match = Regex.Match(ex.Message, @"Limit: (\d+)/1min. Current: (\d+)/1min");
                            if (match.Success && match.Groups.Count == 3)
                            {
                                // The actual limit from the error message (if needed)
                                // int limit = int.Parse(match.Groups[1].Value);
                                int current = int.Parse(match.Groups[2].Value);

                                if (current > limitPerMinute)
                                {



                                    // Calculate the required total wait time in minutes
                                    double totalWaitTimeInMinutes = (double)current / limitPerMinute;


                                    // Subtract 1 minute since we assume that 1 minute has already passed
                                    double delayInMinutes = totalWaitTimeInMinutes - 1;
                                    _logger.LogError($"Attempt {attemptCount}: Rate limit exceeded. Requested {current} images. Need to wait for {delayInMinutes} more minutes.");

                                    if (attemptCount >= maxRetries)
                                    {
                                        throw new Exception($"Failed to complete the image operation after {maxRetries} attempts due to rate limit.", ex);
                                    }

                                    double newDelayInSeconds = delayInMinutes * 60;
                                    // Wait for the calculated delay in minutes before retrying
                                    await Task.Delay(TimeSpan.FromSeconds(delayInSeconds + newDelayInSeconds));
                                }
                            }

                                // Attendre le délai avant de réessayer
                                await Task.Delay(TimeSpan.FromSeconds(delayInSeconds));

                            // Augmenter le délai par 5 secondes pour le prochain essai
                            //delayInSeconds += 5;
                        }
                    }                                    
                    // Pour d'autres erreurs, relancez immédiatement
                    await Task.Delay(TimeSpan.FromSeconds(initialDelayInSeconds));
                    
                }
            }
        }


        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 5, int delayInSeconds = 4)
        {
            int attemptCount = 0;
            while (true)
            {
                try
                {
                    attemptCount++;
                    return await operation();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Attempt {attemptCount}: An error occurred during the operation: {ex.Message}");
                    if (attemptCount >= maxRetries)
                    {
                        throw new Exception($"Failed to complete the operation after {maxRetries} attempts.", ex);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(delayInSeconds));
                }
            }
        }

        public async Task<string> GenerateImageForMealAsyncFlexibleDelay(string chatImagePromptResponse)
        {
            return await ExecuteWithRetryAsyncFlexibledelay(async () =>
            {
                string imageUrl = string.Empty;

                var imageResponse = await _openAiApi.ImageGenerations.CreateImageAsync(
                new ImageGenerationRequest(chatImagePromptResponse, OpenAI_API.Models.Model.DALLE3, ImageSize._1024, "standard"));

                imageUrl = imageResponse.Data[0].Url;
                if (string.IsNullOrEmpty(imageUrl))
                {
                    throw new InvalidOperationException("The image generation response did not include a URL.");
                }

                _logger.LogInformation($"Image successfully generated for meal");
                return imageUrl; // Return the URL of the generated image
            }, maxRetries : 10, initialDelayInSeconds:5);

        }



        public async Task<T> ExecuteWithRetryAsyncFlexibledelay<T>(Func<Task<T>> operation, int maxRetries = 5, int initialDelayInSeconds = 4)
        {
            int attemptCount = 0;
            int delayInSeconds = initialDelayInSeconds; // Définissez le retard initial

            while (true)
            {
                try
                {
                    attemptCount++;
                    return await operation();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Attempt {attemptCount}: An error occurred during the operation: {ex.Message} \n current delay is {delayInSeconds}");
                    if (attemptCount >= maxRetries)
                    {
                        throw new Exception($"Failed to complete the operation after {maxRetries} attempts.", ex);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(delayInSeconds));

                    delayInSeconds += 10; // Augmentez le délai de 10 secondes pour chaque nouvelle tentative
                }
            }
        }


        private Conversation CreateConversation(OpenAI_API.Models.Model model, int tokens)
        {
            _logger.LogInformation("CreateConversation: Starting creation of new conversation");

            try
            {
                var chat = _openAiApi.Chat.CreateConversation();

                chat.RequestParameters.Temperature = 0.5;
                chat.RequestParameters.MaxTokens = tokens;
                chat.Model = model;


                _logger.LogInformation("CreateConversation: Successfully created a new conversation");

                return chat;
            }
            catch (Exception ex)
            {
                _logger.LogError("CreateConversation: An error occurred while creating conversation: {Message}", ex.Message);
                throw;
            }
        }



        static async Task<string> DownloadAndSaveImage(string imageUrl, string imageName)
        {
            string directoryPath = @"C:\persoProjects\MealPlanner\MealgeniusFull\MealGenius_ui\mealsImages\newUserImages";

            string sanitizedImageName = SanitizeFileName(imageName);
            string localFilePath = Path.Combine(directoryPath, sanitizedImageName + ".png");

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(imageUrl);
                if (response.IsSuccessStatusCode)
                {
                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localFilePath, imageBytes);
                    return localFilePath; // Return the local file path
                }
            }

            return null; // Return null if download fails
        }
        // Method to sanitize file names
        static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_'); // Replace invalid chars with underscore
            }
            return fileName;
        }

    }
}
