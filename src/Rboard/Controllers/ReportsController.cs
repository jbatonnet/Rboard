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
    public class ReportsController : Controller
    {
        public ReportService ReportService { get; }

        public ReportsController(ReportService reportService)
        {
            ReportService = reportService;
        }

        public IActionResult Index()
        {
            Report firstReport = ReportService.Reports.First();
            return RedirectToAction(nameof(Show), new { category = firstReport.Category.ToLower(), name = firstReport.Url });
        }

        public IActionResult Show(string category, string name, [FromQuery]bool debug = false, [FromQuery]bool force = false)
        {
            // Try to find the requested report
            Report report = ReportService.FindReport(category, name);
            if (report == null)
                return NotFound("Could not find the specified report");

            ViewData["Debug"] = debug;
            ViewData["Force"] = force;
            ViewData["Reports"] = ReportService.Reports;

            return View(report);
        }
        public IActionResult Raw(string category, string name, [FromQuery]bool force = false)
        {
            // Try to find the requested report
            Report report = ReportService.FindReport(category, name);
            if (report == null)
                return NotFound("Could not find the specified report");
            
            // Update the specified report
            Task<string> reportUpdateTask = ReportService.UpdateReport(report, force);

            // Send the last version to the client
            if (reportUpdateTask.IsCompleted)
            {
                using (StreamReader reader = new StreamReader(reportUpdateTask.Result))
                    return Content(reader.ReadToEnd(), "text/html");
            }
            else
            {
#if DEBUG
                string generatedReport = null;
#else
                string generatedReport = force ? null : ReportService.GetLastGeneratedReport(report);
#endif

                if (generatedReport == null)
                {
                    reportUpdateTask.Wait();
                    generatedReport = reportUpdateTask.Result;
                }

                using (StreamReader reader = new StreamReader(generatedReport))
                    return Content(reader.ReadToEnd(), "text/html");
            }
        }
    }
}