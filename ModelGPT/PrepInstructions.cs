namespace MealGeniusBackend.ModelGPT
{
        public class PrepInstructions
        {
            public string MealName { get; set; }
            public List<GroceryItem> GroceryItems { get; set; }
            public List<string> Instructions { get; set; }
        }
}
