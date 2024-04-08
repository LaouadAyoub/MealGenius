using NLog;
using Polly;
using Polly.Retry;

namespace MealGeniusBackend.Helpers
{
    public static class PollyPolicies
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static readonly AsyncRetryPolicy AnyExceptionRetryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(5, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff formula
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    Logger.Info($"Retry {retryCount} due to {exception.GetType().Name}. Waiting {timespan} before next retry.");
                });
    }


}
