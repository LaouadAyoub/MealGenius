using MealGeniusBackend.DataAcess;

namespace MealGeniusBackend.Models
{
    public class UserTaskDTO
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public UserTaskStatus Status { get; set; }
    }
}
