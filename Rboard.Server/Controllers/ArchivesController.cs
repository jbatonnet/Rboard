using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Rboard.Server.Model;
using Rboard.Server.Services;

namespace Rboard.Server.Controllers
{
    public class ArchivesController : BaseController
    {
        public ArchivesController(ReportService reportService) : base(reportService) { }

        public async Task<IActionResult> Show(string category, string name, DateTime date)
        {
            // Try to find the requested report
            Report report = await ReportService.FindReport(category, name);
            if (report == null)
                return NotFound("Could not find the specified report");

            ViewData["ArchiveDate"] = date;

            return await base.Show(report);
        }
        public async Task<IActionResult> Raw(string category, string name, DateTime date)
        {
            // Try to find the requested report
            Report report = await ReportService.FindReport(category, name);
            if (report == null)
                return NotFound("Could not find the specified report");

            if (report is RReport rReport)
            {
                string content = await ReportService.GetReportArchive(rReport, date);
                if (content == null)
                    return NotFound("Could not find the specified archive");

                return Content(content, "text/html");
            }
            else if (report is ExternalReport externalReport)
            {
                return Redirect(externalReport.Url);
            }
            else
                return NotFound();
        }
    }
}