using Microsoft.Extensions.Configuration;
using Rboard.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Rboard.Services
{
    public class ReportService
    {
        public IEnumerable<Report> Reports
        {
            get
            {
                reloadTask.Wait();
                return reportCategories.SelectMany(p => p.Value);
            }
        }
        public DirectoryInfo ReportsBaseDirectory { get; private set; } = new DirectoryInfo(".");
        public DirectoryInfo ArchiveDirectory { get; private set; } = new DirectoryInfo("Archives");

        internal IConfiguration Configuration { get; }
        internal RService RService { get; }

        private Dictionary<string, List<Report>> reportCategories = new Dictionary<string, List<Report>>();
        private ConcurrentDictionary<Report, Task<string>> reportGenerationTasks = new ConcurrentDictionary<Report, Task<string>>();

        private Task reloadTask;

        public ReportService(IConfiguration configuration, RService rService)
        {
            Configuration = configuration;
            RService = rService;

            Configuration.GetReloadToken().RegisterChangeCallback(s => reloadTask = ReloadConfiguration(), null);
            reloadTask = ReloadConfiguration();
        }

        public Report FindReport(string name)
        {
            reloadTask.Wait();
            return Reports.SingleOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        public Report FindReport(string category, string name)
        {
            reloadTask.Wait();

            List<Report> categoryReports;
            if (!reportCategories.TryGetValue(category.ToLower(), out categoryReports))
                return null;

            return categoryReports.FirstOrDefault(r => string.Equals(r.Url, name, StringComparison.OrdinalIgnoreCase));
        }

        private Task<Report> LoadReport(string category, IConfigurationSection reportSection)
        {
            return Task.Run(() =>
            {
                string url = reportSection.GetValue<string>("Url");
                string path = reportSection.GetValue<string>("Path");
                string refreshTime = reportSection.GetValue<string>("RefreshTime");
                string archiveTime = reportSection.GetValue<string>("ArchiveTime");
                string deleteTime = reportSection.GetValue<string>("DeleteTime");

                Report report = new Report();

                report.Category = category;
                report.Name = Path.GetFileNameWithoutExtension(path);
                report.Url = url;
                report.Path = Path.IsPathRooted(path) ? path : Path.Combine(ReportsBaseDirectory.FullName, path);

                if (refreshTime != null) report.RefreshTime = ParseTime(refreshTime);
                if (archiveTime != null) report.ArchiveTime = ParseTime(archiveTime);
                if (deleteTime != null) report.DeleteTime = ParseTime(deleteTime);

                using (StreamReader reader = new StreamReader(report.Path))
                {
                    string line = reader.ReadLine();

                    if (line == "---")
                    {
                        while (!reader.EndOfStream)
                        {
                            line = reader.ReadLine();
                            if (line == "---")
                                break;

                            int separator = line.IndexOf(':');
                            if (separator < 0)
                                break;

                            string key = line.Remove(separator).Trim();
                            string value = line.Substring(separator + 1).Trim();

                            switch (key)
                            {
                                case "orientation":
                                    report.Configuration[key] = value;
                                    break;
                            }
                        }
                    }
                }

                return report;
            });
        }
        public string GetLastGeneratedReport(Report report)
        {
            reloadTask.Wait();

            FileInfo reportInfo = new FileInfo(report.Path);
            FileInfo generatedReportInfo = new FileInfo(Path.Combine(reportInfo.Directory.FullName, report.GetGeneratedReportName()));

            if (generatedReportInfo.Exists)
                return generatedReportInfo.FullName;
            else
                return null;
        }
        public Task<string> UpdateReport(Report report, bool force = false)
        {
            reloadTask.Wait();

            FileInfo reportInfo = new FileInfo(report.Path);
            FileInfo generatedReportInfo = new FileInfo(Path.Combine(reportInfo.Directory.FullName, report.GetGeneratedReportName()));

            DateTime now = DateTime.Now;

            // Check if archiving is needed
            if (generatedReportInfo.Exists)
            {
                DateTime oldArchiveTime = RoundDateTime(generatedReportInfo.LastWriteTime, report.ArchiveTime);
                DateTime newArchiveTime = RoundDateTime(now, report.ArchiveTime);

                if (newArchiveTime != oldArchiveTime)
                {
                    string archiveName = string.Format("Archive_{0:yyyy-MM-dd}.zip", oldArchiveTime);
                    FileInfo archiveInfo = new FileInfo(Path.Combine(ArchiveDirectory.FullName, archiveName));

                    if (!ArchiveDirectory.Exists)
                        ArchiveDirectory.Create();

                    if (!archiveInfo.Exists)
                    {
                        using (FileStream archiveStream = archiveInfo.Create())
                        using (ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
                            archive.ToString();
                    }

                    using (FileStream archiveStream = archiveInfo.Open(FileMode.Open))
                    using (ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Update))
                    {
                        string reportArchiveName;

                        if (report.ArchiveTime == TimeSpan.FromDays(1))
                            reportArchiveName = string.Format("{0}_{1:yyyy-MM-dd}.html", report.Url, oldArchiveTime);
                        else
                            reportArchiveName = string.Format("{0}_{1:yyyy-MM-dd_HH-mm}.html", report.Url, oldArchiveTime);

                        ZipArchiveEntry reportArchiveEntry = archive.GetEntry(reportArchiveName);
                        if (reportArchiveEntry == null)
                        {
                            reportArchiveEntry = archive.CreateEntry(reportArchiveName, CompressionLevel.Optimal);

                            using (FileStream reportStream = generatedReportInfo.OpenRead())
                            using (Stream reportArchiveStream = reportArchiveEntry.Open())
                                reportStream.CopyTo(reportArchiveStream);
                        }
                    }
                }
            }

            // Check if generation is needed
            if (force)
            {
                Task<string> generationTask;
                if (reportGenerationTasks.TryRemove(report, out generationTask))
                    generationTask.Wait();
            }

            if (!force && generatedReportInfo.Exists && now < generatedReportInfo.LastWriteTime + report.RefreshTime)
                return Task.FromResult(generatedReportInfo.FullName);
            else
                return reportGenerationTasks.GetOrAdd(report, GenerateReport);
        }
        private Task<string> GenerateReport(Report report)
        {
            reloadTask.Wait();

            Task<string> generationTask = Task.Run(() =>
            {
                FileInfo reportInfo = new FileInfo(report.Path);
                FileInfo cleanReportInfo = new FileInfo(Path.Combine(reportInfo.Directory.FullName, Path.ChangeExtension(reportInfo.Name.Replace(" ", "_"), ".g.Rmd")));
                FileInfo generatedReportInfo = new FileInfo(Path.Combine(reportInfo.Directory.FullName, report.GetGeneratedReportName()));

                Console.WriteLine("[{0}] Generating report {1}", DateTime.Now.ToShortTimeString(), reportInfo.Name);

                // Clean info in report
                using (StreamReader reader = reportInfo.OpenText())
                {
                    using (FileStream writerStream = cleanReportInfo.Exists ? cleanReportInfo.OpenWrite() : cleanReportInfo.Create())
                    using (StreamWriter writer = new StreamWriter(writerStream))
                    {
                        string line = reader.ReadLine();

                        // Skip header
                        if (line == "---")
                            while (reader.ReadLine() != "---") ;

                        // Generate custom header
                        writer.WriteLine("---");
                        writer.WriteLine("title: \"{0}\"", report.Name);
                        writer.WriteLine("output:");
                        writer.WriteLine("  flexdashboard::flex_dashboard:");
                        writer.WriteLine("    self_contained: FALSE");
                        writer.WriteLine("    lib_dir: \"libraries\"");
                        writer.WriteLine("    css: \"/libraries/flex-custom.css\"");

                        if (report.Configuration.ContainsKey("orientation"))
                            writer.WriteLine("    orientation: {0}", report.Configuration["orientation"]);

                        writer.WriteLine("---");

                        // Copy the file
                        while (!reader.EndOfStream)
                            writer.WriteLine(reader.ReadLine());
                    }
                }

                // Render the report
                try
                {
                    Task renderTask = RService.Render(cleanReportInfo.FullName, generatedReportInfo.FullName);
                    renderTask.Wait();
                }
                catch (Exception e)
                {
                    Console.WriteLine("[{0}] Error while generating report {1}: {2}", DateTime.Now.ToShortTimeString(), reportInfo.Name, e);
                }

                // Clean everything
                cleanReportInfo.Delete();

                Console.WriteLine("[{0}] Generated report {1}", DateTime.Now.ToShortTimeString(), reportInfo.Name);

                return generatedReportInfo.FullName;
            });

            generationTask.ContinueWith(t => reportGenerationTasks.TryRemove(report, out t));
            return generationTask;
        }

        private Task ReloadConfiguration()
        {
            return Task.Run(() =>
            {
                IConfigurationSection reportsSection = Configuration.GetSection("Reports");
                if (reportsSection != null)
                {
                    IConfigurationSection baseDirectorySection = reportsSection.GetSection("BaseDirectory");
                    if (baseDirectorySection != null)
                        ReportsBaseDirectory = new DirectoryInfo(baseDirectorySection.Value);

                    reportCategories.Clear();
                    foreach (IConfigurationSection categorySection in reportsSection.GetChildren())
                    {
                        string category = categorySection.Key;
                        string categoryKey = category.ToLower();

                        List<Report> categoryReports;
                        if (!reportCategories.TryGetValue(categoryKey, out categoryReports))
                            reportCategories.Add(categoryKey, categoryReports = new List<Report>());

                        foreach (IConfigurationSection child in categorySection.GetChildren())
                        {
                            Task<Report> reportLoadingTask = LoadReport(category, child);
                            reportLoadingTask.Wait();

                            categoryReports.Add(reportLoadingTask.Result);
                        }
                    }
                }
            });
        }

        private static TimeSpan ParseTime(string time)
        {
            time = time.Trim().TrimEnd('s');

            string[] parts = time.Split(' ');
            if (parts.Length != 2)
                throw new FormatException("Could not parse " + time + " as a valid time span");

            int count;
            if (!int.TryParse(parts[0], out count))
                throw new FormatException("Could not parse " + time + " as a valid time span");

            string unit = parts[1].ToLower();
            switch (unit)
            {
                case "second": return TimeSpan.FromSeconds(count);
                case "minute": return TimeSpan.FromMinutes(count);
                case "hour": return TimeSpan.FromHours(count);
                case "day": return TimeSpan.FromDays(count);
                case "week": return TimeSpan.FromDays(7 * count);
                case "month": return TimeSpan.FromDays(30 * count);
                case "year": return TimeSpan.FromDays(365 * count);
            }

            throw new FormatException("Could not parse " + time + " as a valid time span");
        }
        private static DateTime RoundDateTime(DateTime dateTime, TimeSpan interval)
        {
            return new DateTime(dateTime.Ticks - dateTime.Ticks % interval.Ticks);
        }
    }

    public static class ReportExtensions
    {
        public static string GetGeneratedReportName(this Report report)
        {
            return Path.ChangeExtension(Path.GetFileName(report.Path).Replace(" ", "_"), "html");
        }
    }
}