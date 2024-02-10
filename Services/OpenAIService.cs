using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Text;

namespace MealGeniusBackend.Services
{
    public interface IOpenAIService
    {
        Task<string> GetResponseAsync(string systemPrompt, string userPrompt, OpenAI_API.Models.Model model, int tokens);
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
            try
            {
                var chat = CreateConversation(model, tokens);
                chat.AppendSystemMessage(systemPrompt);
                chat.AppendUserInput(userPrompt);
                var response = await chat.GetResponseFromChatbotAsync();
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred while getting a response from the chatbot: {Message}", ex.Message);
                throw;
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
    }
}
