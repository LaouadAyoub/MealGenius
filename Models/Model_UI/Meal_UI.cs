using MealGeniusBackend.Models.Model2ndResponse;
using MealGeniusBackend.Models.ModelGPT;

namespace MealGeniusBackend.Model_UI
{
    public class Meal_UI
    {
        public string MealType { get; set; }
        public string MealName { get; set; }
        public MealMacros Macros { get; set; }
        public List<GroceryItem> GroceryItems { get; set; }
        public List<string> Instructions { get; set; }

    }
}