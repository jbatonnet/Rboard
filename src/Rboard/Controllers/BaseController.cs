using Microsoft.AspNetCore.Mvc;

using Rboard.Model;
using Rboard.Services;

namespace Rboard.Controllers
{
    public abstract class BaseController : Controller
    {
        internal ReportService ReportService { get; }

        protected BaseController(ReportService reportService)
        {
            ReportService = reportService;
        }

        public override ViewResult View()
        {
            ViewData["Reports"] = ReportService.Reports;

            return base.View();
        }

        protected IActionResult Show(Report report)
        {
            ViewData["Reports"] = ReportService.Reports;
            ViewData["ArchiveDates"] = ReportService.EnumerateReportArchives(report, true);

            return View(report);
        }
    }
}