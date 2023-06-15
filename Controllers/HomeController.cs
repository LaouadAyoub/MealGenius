using Microsoft.AspNetCore.Mvc;

namespace MealGeniusBackend.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return PhysicalFile("wwwroot/index.html", "text/html");
        }
    }
}
