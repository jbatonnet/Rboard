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
    public class ArchivesController : BaseController
    {
        public ArchivesController(ReportService reportService) : base(reportService) { }

        public IActionResult Show(string category, string name, DateTime date)
        {
            // Try to find the requested report
            Report report = ReportService.FindReport(category, name);
            if (report == null)
                return NotFound("Could not find the specified report");

            ViewData["ArchiveDate"] = date;

            return base.Show(report);
        }
        public async Task<IActionResult> Raw(string category, string name, DateTime date)
        {
            // Try to find the requested report
            Report report = ReportService.FindReport(category, name);
            if (report == null)
                return NotFound("Could not find the specified report");

            string content = await ReportService.GetReportArchive(report, date);
            if (content == null)
                return NotFound("Could not find the specified archive");

            return Content(content, "text/html");
        }
    }
}