using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Hangfire;
using Microsoft.Owin;
using Owin;
using WebApplication5.Models;

[assembly: OwinStartup(typeof(WebApplication5.Startup))] // Use your actual namespace


namespace WebApplication5
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Setup Hangfire with SQL Server
            GlobalConfiguration.Configuration
                .UseSqlServerStorage("HangfireConnection"); // Your connection string

            // Enable Hangfire Dashboard and Server
            app.UseHangfireDashboard("/hangfire");
            app.UseHangfireServer();

            // Register recurring job
            RecurringJob.AddOrUpdate<StockAlertService>(
                "low-stock-check",
                x => x.CheckLowStock(),
                 //Cron.Hourly // or Cron.Minutely
                 "0 10 * * *"
            );
        }
    }
}