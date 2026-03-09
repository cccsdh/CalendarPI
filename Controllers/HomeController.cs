using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace CalendarPi.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _config;

        public HomeController(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index()
        {
            var pos = _config.GetValue<string>("Clock:Position") ?? "bottom-left";
            ViewData["ClockPosition"] = pos;
            return View();
        }

        public IActionResult Error()
        {
            return Problem();
        }
    }
}
