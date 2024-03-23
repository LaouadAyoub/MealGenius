using System;

public static class CaloricNeedsCalculator
{
    // Enum to represent different activity levels.
    public enum ActivityLevel
    {
        Sedentary,
        LightlyActive,
        ModeratelyActive,
        VeryActive,
        ExtraActive
    }

    // Method to calculate BMR using Mifflin-St Jeor equation.
    public static double CalculateBMR(double weight, double height, int age, string sex)
    {
        double maleBMR = (10 * weight) + (6.25 * height) - (5 * age) + 5;
        double femaleBMR = (10 * weight) + (6.25 * height) - (5 * age) - 161;

        if (sex == "Male")
        {
            return (10 * weight) + (6.25 * height) - (5 * age) + 5;
        }
        else if (sex == "Female")
        {
            return (10 * weight) + (6.25 * height) - (5 * age) - 161;
        }
        else
        {
            return (10 * weight) + (6.25 * height) - (5 * age) - 78;
        }
    }

    // Method to calculate daily caloric needs based on BMR and activity level.
    public static double CalculateDailyCaloricNeeds(double bmr, ActivityLevel activityLevel)
    {
        switch (activityLevel)
        {
            case ActivityLevel.Sedentary:
                return bmr * 1.2;
            case ActivityLevel.LightlyActive:
                return bmr * 1.375;
            case ActivityLevel.ModeratelyActive:
                return bmr * 1.55;
            case ActivityLevel.VeryActive:
                return bmr * 1.725;
            case ActivityLevel.ExtraActive:
                return bmr * 1.9;
            default:
                throw new ArgumentException("Invalid activity level provided.");
        }
    }

    // Method to adjust daily caloric needs based on weight management goals.
    public static double AdjustCaloriesForWeightGoal(double dailyCaloricNeeds, int calorieAdjustment)
    {
        return dailyCaloricNeeds + calorieAdjustment;
    }
}
