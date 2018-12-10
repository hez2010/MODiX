﻿using System;
using System.Diagnostics;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modix.Data.Models.Core;
using Modix.Services.CodePaste;
using Modix.Services.Utilities;
using Serilog;
using Serilog.Events;

namespace Modix
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables("MODIX_")
                .AddJsonFile("developmentSettings.json", optional: true, reloadOnChange: false)
                .Build();

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Modix.DiscordSerilogAdapter", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.RollingFile(@"logs\{Date}", restrictedToMinimumLevel: LogEventLevel.Debug);

            var webhookId = config.GetValue<ulong>(nameof(ModixConfig.LogWebhookId));
            var webhookToken = config.GetValue<string>(nameof(ModixConfig.LogWebhookToken));

            var webHost = CreateWebHostBuilder(args, config).Build();

            if (webhookId != default && string.IsNullOrWhiteSpace(webhookToken) == false)
            {
                loggerConfig = loggerConfig
                    .WriteTo.DiscordWebhookSink(webhookId, webhookToken, LogEventLevel.Error, webHost.Services.GetRequiredService<CodePasteService>());
            }

            Log.Logger = loggerConfig.CreateLogger();

            try
            {
                webHost.Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.ForContext<Program>()
                    .Fatal(ex, "Host terminated unexpectedly.");

                if (Debugger.IsAttached && Environment.UserInteractive)
                {
                    Console.WriteLine(Environment.NewLine + "Press any key to exit...");
                    Console.ReadKey(true);
                }

                return ex.HResult;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args, IConfiguration config) =>
            WebHost.CreateDefaultBuilder(args)
                .UseConfiguration(config)
                .UseSerilog()
                .UseStartup<Startup>();
    }
}
