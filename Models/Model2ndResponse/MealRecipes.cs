using MealGeniusBackend.Models.ModelGPT;

namespace MealGeniusBackend.Models.Model2ndResponse
{
    public class MealRecipes
    {
        public string MealName { get; set; }
        public List<GroceryItem> GroceryItems { get; set; }
        public List<string> Instructions { get; set; }
        public MealMacros MealMacros { get; set; }
    }
}