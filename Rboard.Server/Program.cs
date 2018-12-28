using System;
using System.IO;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Rboard.Server.Services;

namespace Rboard.Server
{
    public class Program
    {
        public static ushort ServerPort { get; private set; } = 80;

        public static void Main(string[] args)
        {
            string serverPortText = Environment.GetEnvironmentVariable("RBOARD_SERVER_PORT");
            if (!string.IsNullOrEmpty(serverPortText) && ushort.TryParse(serverPortText, out ushort serverPort))
                ServerPort = serverPort;

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables(prefix: "RBOARD")
                .Build();

            var host = WebHost.CreateDefaultBuilder(args)
                .UseKestrel()
                .ConfigureAppConfiguration((hostingContext, configurationBuilder) =>
                {
                    var environment = hostingContext.HostingEnvironment;
                    configurationBuilder.AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                })
                .UseConfiguration(configuration)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .UseUrls($"http://*:{ServerPort}")
                .Build();

            // Preinitialize some services
            host.Services.GetService<RService>();
            host.Services.GetService<ReportService>();

            host.Run();
        }
    }
}
