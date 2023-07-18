namespace MealGeniusBackend.Models.ModelGPT
{
    public class PrepInstructions
    {
        public string MealName { get; set; }
        public List<GroceryItem> GroceryItems { get; set; }
        public List<string> Instructions { get; set; }
        public Macros MealMacros { get; set; }
    }
    public class Meal
    {
        public string MealType { get; set; }

        public string MealName { get; set; }

    }
}
