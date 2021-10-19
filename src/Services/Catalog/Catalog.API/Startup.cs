﻿using System.IO;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnDapr.BuildingBlocks.EventBus;
using Microsoft.eShopOnDapr.BuildingBlocks.EventBus.Abstractions;
using Microsoft.eShopOnDapr.Services.Catalog.API.Infrastructure;
using Microsoft.eShopOnDapr.Services.Catalog.API.IntegrationEvents.EventHandling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Microsoft.eShopOnDapr.Services.Catalog.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().AddDapr();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "eShopOnDapr - Catalog API", Version = "v1" });
            });

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                    .SetIsOriginAllowed((host) => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });

            var healthChecksBuilder = services.AddHealthChecks();
            healthChecksBuilder
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddDapr()
                .AddSqlServer(
                    Configuration["SqlConnectionString"],
                    name: "CatalogDB-check",
                    tags: new string[] { "catalogdb" });

            services.AddScoped<IEventBus, DaprEventBus>();
            services.AddScoped<OrderStatusChangedToAwaitingStockValidationIntegrationEventHandler>();
            services.AddScoped<OrderStatusChangedToPaidIntegrationEventHandler>();

            services.AddDbContext<CatalogDbContext>(
                options => options.UseSqlServer(Configuration["SqlConnectionString"]));
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog.API V1");
                });
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(env.ContentRootPath, "Pics")),
                RequestPath = "/pics"
            });

            app.UseRouting();
            app.UseCloudEvents();
            
            app.UseCors("CorsPolicy");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
                endpoints.MapControllers();
                endpoints.MapSubscribeHandler();
                endpoints.MapHealthChecks("/hc", new HealthCheckOptions()
                {
                    Predicate = _ => true,
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });
                endpoints.MapHealthChecks("/liveness", new HealthCheckOptions
                {
                    Predicate = r => r.Name.Contains("self")
                });
            });
        }
    }
}
