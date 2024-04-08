using Newtonsoft.Json;

namespace MealGeniusBackend.Models
{
    public class PaymentRegistration
    {
        [JsonProperty("email")]
        public string Email { get; set; }
        [JsonProperty("paymentID")]
        public string PaymentID { get; set; }
        [JsonProperty("paymentAmount")]
        public string PaymentAmount { get; set; }

        [JsonProperty("paymentCurrency")]
        public string PaymentCurrency { get; set; }

        [JsonProperty("paymentDate")]
        public string PaymentDate { get; set; }

        [JsonProperty("paymentStatus")]
        public string Country { get; set; }
    }

}
