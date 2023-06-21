namespace MealGeniusBackend.Model
{
    public class WeekPlan
    {
        public Meal Monday { get; set; }
        public Meal Tuesday { get; set; }
        public Meal Wednesday { get; set; }
        public Meal Thursday { get; set; }
        public Meal Friday { get; set; }
        public Meal Saturday { get; set; }
        public Meal Sunday { get; set; }
        public string endFlag { get; set; }
    }
}