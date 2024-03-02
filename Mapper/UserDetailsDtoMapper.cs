using MealGeniusBackend.Models;

namespace MealGeniusBackend.Mapper
{
    public static class DtoMapper
    {
        public static UserDetailsDtoLite MapToLite(UserDetailsDto userDetails)
        {
            return new UserDetailsDtoLite
            {
                Name = userDetails.Name,
                Age = userDetails.Age,
                Sex = userDetails.Sex,
                GenderIdentity = userDetails.GenderIdentity,
                Weight = userDetails.Weight,
                Height = userDetails.Height,
                ActivityLevel = userDetails.ActivityLevel
            };
        }
    }
}
