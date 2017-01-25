using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Rboard.Services
{
    public class RService
    {
        public string RExecutable { get; private set; }
        public string[] RPackages { get; private set; }

        internal IConfiguration Configuration { get; }

        private Task reloadTask;
        private Task packageInstallationTask;
        private object renderLock = new object();

        public RService(IConfiguration configuration)
        {
            Configuration = configuration;

            Configuration.GetReloadToken().RegisterChangeCallback(s => reloadTask = ReloadConfiguration(), null);
            reloadTask = ReloadConfiguration();

            packageInstallationTask = InstallPackages(RPackages);
        }

        public string GetRExecutablePath()
        {
            reloadTask.Wait();

            string rExecutable = RExecutable;

            if (File.Exists(rExecutable))
                return rExecutable;

            string extension = Path.GetExtension(rExecutable);
            if (extension == string.Empty)
                rExecutable += ".exe";

            rExecutable = Environment
                .GetEnvironmentVariable("PATH")
                .Split(';')
                .Where(p => !p.ToLower().Contains("houdini"))
                .Select(p => Path.Combine(p, rExecutable))
                .FirstOrDefault(p => File.Exists(p));

            if (rExecutable == null)
                throw new FileNotFoundException("Could not find R executable path");

            return rExecutable;
        }

        public bool IsPackageInstalled(string packageName)
        {
            throw new NotImplementedException();
        }
        public Task InstallPackages(params string[] packageNames)
        {
            reloadTask.Wait();

            string rExecutable = GetRExecutablePath();

            return Task.Run(() =>
            {
                foreach (string package in RPackages)
                {
                    string arguments = string.Format("-e \"if (!('{0}' %in% rownames(installed.packages()))) install.packages('{0}')\"", package);

                    ProcessStartInfo processStartInfo = new ProcessStartInfo(rExecutable, arguments)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    Process process = Process.Start(processStartInfo);
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        throw new Exception("Error while installing package " + package);
                }
            });
        }

        public Task Render(string sourcePath, string destinationPath)
        {
            reloadTask.Wait();
            packageInstallationTask.Wait();

            return Task.Run(() =>
            {
                string rExecutable = GetRExecutablePath();

                string generationParameters = string.Format("-e \"rmarkdown::render('{0}', output_file = '{1}', quiet = TRUE)\"",
                    sourcePath.Replace("\\", "\\\\"),
                    destinationPath.Replace("\\", "\\\\"));

                ProcessStartInfo processStartInfo = new ProcessStartInfo(rExecutable, generationParameters)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = Path.GetDirectoryName(sourcePath)
                };

                lock (renderLock)
                {
                    Process process = Process.Start(processStartInfo);
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception("Error while generating report " + Path.GetFileName(sourcePath) + ": " + error);
                    }
                }
            });
        }

        private Task ReloadConfiguration()
        {
            return Task.Run(() =>
            {
                IConfigurationSection rSection = Configuration.GetSection("R");
                if (rSection != null)
                {
                    foreach (IConfigurationSection child in rSection.GetChildren())
                    {
                        switch (child.Key)
                        {
                            case "Executable":
                                RExecutable = child.Value;
                                break;

                            case "Packages":
                                RPackages = child.GetChildren().Select(c => c.Value).ToArray();
                                break;
                        }
                    }
                }
            });
        }
    }
}