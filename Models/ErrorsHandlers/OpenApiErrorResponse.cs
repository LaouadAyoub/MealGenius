using Newtonsoft.Json;

namespace MealGeniusBackend.Models.ErrorsHandlers
{
    public class OpenApiErrorResponse
    {
        [JsonProperty("error")]
        public ApiErrorDetail Error { get; set; }
    }

    public class ApiErrorDetail
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("param")]
        public string Param { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

}
