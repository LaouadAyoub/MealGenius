using MealGeniusBackend.ModelGPT;

namespace MealGeniusBackend.Model_UI
{
    public class Meal_UI
    {
        public string MealType { get; set; }
        public string MealName { get; set; }
        public List<GroceryItem> GroceryItems { get; set; }
        public List<string> Instructions { get; set; }
    }
}