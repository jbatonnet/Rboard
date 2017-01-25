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
            services.AddSingleton<IConfiguration>(Configuration);

            // Add framework services.
            services.AddMvc();

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

            app.UseStaticFiles();
            app.UseStaticFiles("/assets");
            app.UseStaticFiles("/libraries");

            app.UseMvc(routes =>
            {
                routes.MapRoute("Reports", "reports/{category}/{name}.html", new { controller = "Reports", action = "Raw" });
                routes.MapRoute("Archives", "archives/{category}/{name}.html", new { controller = "Archives", action = "Raw" });
                routes.MapRoute("Pages", "{category}/{name}.html", new { controller = "Reports", action = "Show" });

                routes.MapRoute("Reports.Files", "reports/{category}/libraries/{*path}", new { controller = "Files", action = "Download" });
                routes.MapRoute("Archives.Files", "archives/{category}/libraries/{*path}", new { controller = "Files", action = "Download" });
                routes.MapRoute("Pages.Files", "{category}/libraries/{*path}", new { controller = "Files", action = "Download" });

                routes.MapRoute("Index", "", new { controller = "Reports", action = "Index" });
            });
        }
    }
}
