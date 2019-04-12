﻿using System;
using System.Linq;
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
            ViewData["ArchiveDates"] = report is RReport rReport ? ReportService.EnumerateReportArchives(rReport, true) : Enumerable.Empty<DateTime>();

            return View(report);
        }
    }
}