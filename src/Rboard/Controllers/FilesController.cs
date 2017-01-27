using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Rboard.Model;
using System.IO;
using Rboard.Services;

namespace Rboard.Controllers
{
    public class FilesController : Controller
    {
        public IActionResult Download(string path, string category = null)
        {
            return RedirectPermanent("/assets/" + path);
        }
    }
}