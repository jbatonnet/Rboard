using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Rboard.Model;
using System.IO;
using Rboard.Services;
using Microsoft.Extensions.Configuration;

namespace Rboard.Controllers
{
    public abstract class BaseController : Controller
    {
        internal ReportService ReportService { get; }

        protected BaseController(ReportService reportService)
        {
            ReportService = reportService;
        }

        protected IActionResult Show(Report report)
        {
            ViewData["Reports"] = ReportService.Reports;
            ViewData["ArchiveDates"] = ReportService.EnumerateReportArchives(report, true);

            return View(report);
        }
    }
}