using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;

namespace MealGeniusBackend.Services;

public interface IOpenAIService
{
    Task<string> GetResponseAsync(string systemPrompt, string userPrompt, int tokens);
    Task<string> GenerateJsonBasedOnPromptResponseAsync(string systemPrompt, string userPrompt, int maxTokens, double temperature = 0);
    Task<byte[]> GenerateImageForMealAsync(string prompt);
}

public sealed class OpenAIConcurrency(IConfiguration configuration) : IDisposable
{
    public SemaphoreSlim Gate { get; } = new(Math.Clamp(configuration.GetValue("OpenAI:MaxConcurrency", 3), 1, 12));
    public void Dispose() => Gate.Dispose();
}

public class OpenAIService(HttpClient http, IConfiguration configuration, OpenAIConcurrency concurrency,
    GenerationCancellation cancellation, ILogger<OpenAIService> logger) : IOpenAIService
{
    public Task<string> GetResponseAsync(string systemPrompt, string userPrompt, int tokens) =>
        Chat(systemPrompt, userPrompt, tokens, false, 0.5);

    public Task<string> GenerateJsonBasedOnPromptResponseAsync(string systemPrompt, string userPrompt,
        int maxTokens, double temperature = 0) => Chat(systemPrompt, userPrompt, maxTokens, true, temperature);

    private async Task<string> Chat(string systemPrompt, string userPrompt, int tokens, bool json, double temperature)
    {
        var body = new JsonObject
        {
            ["model"] = configuration[json ? "OpenAI:JsonModel" : "OpenAI:TextModel"] ?? "gpt-4.1-mini",
            ["temperature"] = temperature,
            ["max_tokens"] = Math.Max(tokens, json ? 4000 : tokens),
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userPrompt })
        };
        if (json) body["response_format"] = new JsonObject { ["type"] = "json_object" };
        using var response = await Send("chat/completions", body);
        var choice = response.RootElement.GetProperty("choices")[0];
        if (choice.GetProperty("finish_reason").GetString() != "stop")
            throw new InvalidDataException("AI response was incomplete or refused.");
        var text = choice.GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("AI returned empty content.");
        if (json) ValidateJson(text);
        return text;
    }

    public async Task<byte[]> GenerateImageForMealAsync(string prompt)
    {
        using var response = await Send("images/generations", new JsonObject
        {
            ["model"] = configuration["OpenAI:ImageModel"] ?? "gpt-image-2",
            ["prompt"] = prompt, ["n"] = 1, ["size"] = "1024x1024", ["quality"] = "low",
            ["output_format"] = "jpeg"
        });
        var encoded = response.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString();
        if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidDataException("AI returned no image.");
        return Convert.FromBase64String(encoded);
    }

    public static void ValidateJson(string text)
    {
        try { JObject.Parse(text); }
        catch (Newtonsoft.Json.JsonException ex) { throw new InvalidDataException("AI returned malformed JSON.", ex); }
    }

    private async Task<JsonDocument> Send(string endpoint, JsonObject body)
    {
        var key = configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Missing OPENAI_API_KEY.");
        var token = cancellation.Token;
        var attempts = Math.Clamp(configuration.GetValue("OpenAI:MaxAttempts", 3), 1, 5);
        await concurrency.Gate.WaitAsync(token);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/" + endpoint)
                    {
                        Content = JsonContent.Create(body)
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                    using var response = await http.SendAsync(request, token);
                    if (response.IsSuccessStatusCode)
                        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                    if (!IsTransient(response.StatusCode) || attempt >= attempts)
                        throw new HttpRequestException($"OpenAI request failed with HTTP {(int)response.StatusCode}.", null, response.StatusCode);
                    logger.LogWarning("Transient OpenAI HTTP {Status}; attempt {Attempt}/{Attempts}.",
                        (int)response.StatusCode, attempt, attempts);
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(retryAfter.TotalSeconds, 1, 30)), token);
                }
                catch (HttpRequestException ex) when (ex.StatusCode is null && attempt < attempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), token);
                }
                catch (TaskCanceledException) when (!token.IsCancellationRequested && attempt < attempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), token);
                }
            }
        }
        finally { concurrency.Gate.Release(); }
    }
    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)status >= 500;
}
