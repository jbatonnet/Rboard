using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Rboard.Server.Services
{
    public class RService
    {
        public string RScriptExecutable { get; private set; } = "Rscript";
        public string PandocExecutable { get; private set; } = "pandoc";

        public string[] RPackages { get; private set; } = new string[0];

        internal IConfigurationRoot Configuration { get; }
        internal ILogger<RService> Logger { get; }

        private Task reloadTask;
        private Task packageInstallationTask;

        private object renderLock = new object();
        private object installationLock = new object();

        public RService(IConfigurationRoot configuration, ILogger<RService> logger)
        {
            Configuration = configuration;
            Logger = logger;

            Configuration.GetReloadToken().RegisterChangeCallback(s => reloadTask = ReloadConfiguration(), null);
            reloadTask = ReloadConfiguration();

            packageInstallationTask = reloadTask.ContinueWith(async t =>
            {
                await InstallPackages("rmarkdown", "flexdashboard");
                await InstallPackages(RPackages);
            });
        }

        public string GetRScriptExecutablePath()
        {
            string rScriptExecutable = RScriptExecutable;

            if (File.Exists(rScriptExecutable))
                return rScriptExecutable;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                string extension = Path.GetExtension(rScriptExecutable);
                if (extension == string.Empty)
                    rScriptExecutable += ".exe";
            }

            // Try to find it in the PATH
            string rScriptPath = Environment
                .GetEnvironmentVariable("PATH")
                .Split(new char[] { ';', ':' })
                .Where(p => !p.ToLower().Contains("houdini")) // Houdini also has an rscript binary
                .Select(p => Path.Combine(p, rScriptExecutable))
                .FirstOrDefault(p => File.Exists(p));

            if (rScriptPath != null)
                return rScriptPath;

            // Then try to find R in registry if available
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(Environment.Is64BitProcess ? @"SOFTWARE\R-core\R64" : @"SOFTWARE\R-core\R");
                if (key != null)
                {
                    string rPath = key.GetValue("InstallPath", null) as string;
                    if (!string.IsNullOrEmpty(rPath))
                    {
                        rScriptPath = Path.Combine(rPath, "bin", rScriptExecutable);
                        if (File.Exists(rScriptPath))
                            return rScriptPath;
                    }
                }
            }
            catch { }

            throw new FileNotFoundException("Could not find R executable path");
        }
        public string GetPandocExecutablePath()
        {
            string pandocExecutable = PandocExecutable;

            if (File.Exists(pandocExecutable))
                return pandocExecutable;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                string extension = Path.GetExtension(pandocExecutable);
                if (extension == string.Empty)
                    pandocExecutable += ".exe";
            }

            // Try to find pandoc in the PATH
            string pandocPath = Environment
                .GetEnvironmentVariable("PATH")
                .Split(new char[] { ';', ':' })
                .Select(p => Path.Combine(p, pandocExecutable))
                .FirstOrDefault(p => File.Exists(p));

            if (pandocPath != null)
                return pandocPath;

            // Then try to find it in default install location
            try
            {
                string programPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrEmpty(programPath))
                {
                    pandocPath = Path.Combine(programPath, "Pandoc", pandocExecutable);
                    if (File.Exists(pandocPath))
                        return pandocPath;
                }
            }
            catch { }

            throw new FileNotFoundException("Could not find Pandoc executable path");
        }

        public async Task<bool> IsPackageInstalled(string packageName)
        {
            await reloadTask;

            Logger.LogTrace($"Checking package installation for {packageName}");

            string rScriptExecutable = GetRScriptExecutablePath();
            string arguments = $"-e \"if (!('{packageName}' %in% rownames(installed.packages()))) quit(save = 'no', status = -1)\"";

            ProcessStartInfo processStartInfo = new ProcessStartInfo(rScriptExecutable, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            lock (renderLock)
            {
                Process process = Process.Start(processStartInfo);
                process.WaitForExit();

                int exitCode = process.ExitCode;
                bool? result = null;

                if (exitCode == 0) result = true;
                if (exitCode == -1) result = false;
                if (exitCode == 255) result = false;

                if (result != null)
                {
                    Logger.LogTrace($"Package {packageName} is {(result.Value ? "" : "not ")}installed");
                    return result.Value;
                }

                string error = process.StandardError.ReadToEnd();
                throw new Exception("Error while checking installed package " + packageName + ": " + error);
            }
        }
        public async Task InstallPackages(params string[] packageNames)
        {
            if (packageNames == null || packageNames.Length == 0)
                return;

            await reloadTask;

            string rScriptExecutable = GetRScriptExecutablePath();

            foreach (string packageName in packageNames)
            {
                if (await IsPackageInstalled(packageName))
                    continue;

                lock (installationLock)
                {
                    Logger.LogInformation($"Installing package {packageName}");

                    string arguments = $"-e \"if (!('{packageName}' %in% rownames(installed.packages()))) install.packages('{packageName}', repos='http://cran.rstudio.com/')\"";

                    ProcessStartInfo processStartInfo = new ProcessStartInfo(rScriptExecutable, arguments)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                    };

                    Process process = Process.Start(processStartInfo);
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception("Error while installing package " + packageName + ": " + error);
                    }

                    Logger.LogInformation($"Installed package {packageName}");
                }
            }
        }

        public async Task WaitUntilReady()
        {
            await reloadTask;
            await packageInstallationTask;
        }
        public async Task Render(string sourcePath, string destinationPath)
        {
            await reloadTask;
            await packageInstallationTask;

            string pandocExecutable = GetPandocExecutablePath();
            string rExecutable = GetRScriptExecutablePath();

            string fileName = Path.GetFileName(sourcePath);

            string generationParameters = string.Format("-e \"Sys.setenv(RSTUDIO_PANDOC = '{2}')\" -e \"rmarkdown::render('{0}', output_file = '{1}', quiet = TRUE)\"",
                sourcePath.Replace("\\", "\\\\"),
                destinationPath.Replace("\\", "\\\\"),
                Path.GetDirectoryName(pandocExecutable).Replace("\\", "\\\\"));

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
                Logger.LogDebug($"Rendering file {fileName}");

                Process process = Process.Start(processStartInfo);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new Exception("Error while generating report " + Path.GetFileName(sourcePath) + ": " + error);
                }

                Logger.LogDebug($"Successfully rendered file {fileName}");
            }
        }

        private Task ReloadConfiguration()
        {
            return Task.Run(() =>
            {
                Logger.LogDebug("Reloading configuration");

                IConfigurationSection rSection = Configuration.GetSection("R");
                if (rSection != null)
                {
                    foreach (IConfigurationSection child in rSection.GetChildren())
                    {
                        switch (child.Key)
                        {
                            case "RScriptExecutable":
                                RScriptExecutable = child.Value;
                                break;

                            case "PandocExecutable":
                                PandocExecutable = child.Value;
                                break;

                            case "Packages":
                                RPackages = child.GetChildren().Select(c => c.Value).ToArray();
                                break;
                        }
                    }
                }

                Logger.LogDebug("Reloaded configuration");
            });
        }
    }
}