using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Rboard.Server.Services;

namespace Rboard.Server
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration as IConfigurationRoot;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(Configuration);

            // Add framework services.
            services.AddMvc();

            // Add reporting services.
            services.AddSingleton<RService>();
            services.AddSingleton<ReportService>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                //app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

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
