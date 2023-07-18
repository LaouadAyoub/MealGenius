using MealGeniusBackend.Model_UI;

namespace MealGeniusBackend.Models.Model_UI
{
    public class DayMealPlan_UI
    {
        public string DayName { get; set; }
        public List<Meal_UI> Meals { get; set; }
    }
}