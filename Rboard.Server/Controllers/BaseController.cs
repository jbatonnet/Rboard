using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Rboard.Server.Model;
using Rboard.Server.Services;

namespace Rboard.Server.Controllers
{
    public abstract class BaseController : Controller
    {
        internal ReportService ReportService { get; }

        protected BaseController(ReportService reportService)
        {
            ReportService = reportService;
        }

        protected async Task<IActionResult> Show(Report report)
        {
            ViewData["Reports"] = await ReportService.GetReports();
            ViewData["ArchiveDates"] = ReportService.EnumerateReportArchives(report, true);

            return View(report);
        }
    }
}