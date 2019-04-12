using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

using Rboard.Server.Model;
using Rboard.Server.Services;

namespace Rboard.Server.Controllers
{
    public enum SlideshowMode
    {
        Disabled,
        SingleReport,
        AllReports,
        FirstReports,
        CategoryReports
    }

    public class ReportsController : BaseController
    {
        public SlideshowMode SlideshowMode { get; private set; } = SlideshowMode.SingleReport;
        public TimeSpan SlideshowTime { get; private set; } = TimeSpan.FromSeconds(20);

        public bool DebugMode
        {
            get
            {
                string debugMode = Request.Cookies[nameof(DebugMode)];

#if DEBUG
                if (debugMode == null)
                    return false;
#endif

                return debugMode == "true";
            }
            set
            {
                Response.Cookies.Append(nameof(DebugMode), value ? "true" : "false");
            }
        }
        public bool PauseMode
        {
            get
            {
                string pauseMode = Request.Cookies[nameof(PauseMode)];

#if DEBUG
                if (pauseMode == null)
                    return true;
#endif

                return pauseMode == "true";
            }
            set
            {
                Response.Cookies.Append(nameof(PauseMode), value ? "true" : "false");
            }
        }

        internal IConfigurationRoot Configuration { get; }

        private Task reloadTask;

        public ReportsController(IConfigurationRoot configuration, ReportService reportService) : base(reportService)
        {
            Configuration = configuration;

            Configuration.GetReloadToken().RegisterChangeCallback(s => reloadTask = ReloadConfiguration(), null);
            reloadTask = ReloadConfiguration();
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Reports"] = await ReportService.GetReports();

            return View();
        }

        public IActionResult TogglePause(string category, string name)
        {
            PauseMode = !PauseMode;

            return RedirectToAction(nameof(Show), new { category = category, name = name });
        }
        public IActionResult ToggleDebug(string category, string name)
        {
            DebugMode = !DebugMode;

            return RedirectToAction(nameof(Show), new { category = category, name = name });
        }
        public async Task<IActionResult> ForceReload(string category, string name)
        {
            await ReportService.ReloadReports();

            return RedirectToAction(nameof(Show), new { category = category, name = name, force = true });
        }

        public async Task<IActionResult> Show(string category, string name, [FromQuery]bool force = false)
        {
            // Try to find the requested report
            Report report = await ReportService.FindReport(category, name);
            if (report == null)
                return RedirectToAction(nameof(Index));

            ViewData["SlideshowMode"] = SlideshowMode;
            ViewData["SlideshowTime"] = SlideshowTime;

            ViewData["DebugMode"] = DebugMode;
            ViewData["PauseMode"] = PauseMode;

            ViewData["Force"] = force;

            return await base.Show(report);
        }
        public async Task<IActionResult> Raw(string category, string name, [FromQuery]bool force = false)
        {
            // Try to find the requested report
            Report report = await ReportService.FindReport(category, name);
            if (report == null)
                return RedirectToAction(nameof(Index));

            if (report is RReport rReport)
            {
                // Update the specified report
                Task<string> reportUpdateTask = ReportService.UpdateReport(rReport, force);

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
                    string generatedReport = force ? null : await ReportService.GetLastGeneratedReport(rReport);
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
            else if (report is ExternalReport externalReport)
                return Redirect(externalReport.Url);
            else
                return RedirectToAction(nameof(Index));
        }

        private Task ReloadConfiguration()
        {
            return Task.Run(() =>
            {
                IConfigurationSection reportsSection = Configuration.GetSection("Rboard");
                if (reportsSection != null)
                {
                    IConfigurationSection slideshowModeSection = reportsSection.GetSection(nameof(SlideshowMode));
                    if (slideshowModeSection?.Value != null)
                        SlideshowMode = (SlideshowMode)Enum.Parse(typeof(SlideshowMode), slideshowModeSection.Value);

                    IConfigurationSection slideshowTimeSection = reportsSection.GetSection(nameof(SlideshowTime));
                    if (slideshowTimeSection?.Value != null)
                        SlideshowTime = Utils.ParseTime(slideshowTimeSection.Value);
                }
            });
        }
    }
}