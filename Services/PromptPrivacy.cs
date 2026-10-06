using Newtonsoft.Json.Linq;
namespace MealGeniusBackend.Services;
public static class PromptPrivacy
{
    public static string RemoveEmail(string json)
    {
        var input = JObject.Parse(json);
        if (input["Details"] is JObject details) details.Remove("Email");
        return input.ToString(Newtonsoft.Json.Formatting.None);
    }
}
