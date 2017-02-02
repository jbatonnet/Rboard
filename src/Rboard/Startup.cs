using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rboard.Model;
using Rboard.Services;

namespace Rboard
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IConfigurationRoot>(Configuration);

            // Add framework services.
            services.AddMvc();
            services.AddSession();

            // Add reporting services.
            services.AddSingleton<RService>();
            services.AddSingleton<ReportService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseSession();

            app.UseStaticFiles();
            app.UseStaticFiles("/assets");
            app.UseStaticFiles("/libraries");

            app.UseMvc(routes =>
            {
                // Main URLs
                routes.MapRoute("Reports", "{category}/{name}.html", new { controller = "Reports", action = "Show" });
                routes.MapRoute("Archives", "archives/{category}/{name}_{date}.html", new { controller = "Archives", action = "Show" });

                // Raw reports
                routes.MapRoute("Reports.Raw", "raw/{category}/{name}.html", new { controller = "Reports", action = "Raw" });
                routes.MapRoute("Archives.Raw", "raw/archives/{category}/{name}_{date}.html", new { controller = "Archives", action = "Raw" });

                // Actions
                routes.MapRoute("Reports.ToggleDebug", "reports/debug/{category}/{name}.html", new { controller = "Reports", action = "ToggleDebug" });
                routes.MapRoute("Reports.TogglePause", "reports/pause/{category}/{name}.html", new { controller = "Reports", action = "TogglePause" });
                routes.MapRoute("Reports.ForceReload", "reports/reload/{category}/{name}.html", new { controller = "Reports", action = "ForceReload" });

                // Assets
                routes.MapRoute("Reports.Assets", "{category}/libraries/{*path}", new { controller = "Files", action = "Download" });
                routes.MapRoute("Archives.Assets", "archives/{category}/libraries/{*path}", new { controller = "Files", action = "Download" });
                routes.MapRoute("Reports.Raw.Assets", "raw/{category}/libraries/{*path}", new { controller = "Files", action = "Download" });
                routes.MapRoute("Archives.Raw.Assets", "raw/archives/{category}/libraries/{*path}", new { controller = "Files", action = "Download" });

                routes.MapRoute("Index", "", new { controller = "Reports", action = "Index" });
            });
        }
    }
}
