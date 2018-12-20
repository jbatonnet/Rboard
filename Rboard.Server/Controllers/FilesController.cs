using Microsoft.AspNetCore.Mvc;

namespace Rboard.Server.Controllers
{
    public class FilesController : Controller
    {
        public IActionResult Download(string path, string category = null)
        {
            return RedirectPermanent("/assets/" + path);
        }
    }
}