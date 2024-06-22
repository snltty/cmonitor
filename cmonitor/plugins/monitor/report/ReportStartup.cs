﻿using cmonitor.config;
using cmonitor.plugins.report.messenger;
using cmonitor.startup;
using common.libs;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace cmonitor.plugins.report
{
    public sealed class ReportStartup : IStartup
    {
        public StartupLevel Level => StartupLevel.Normal;
        public string Name => "report";

        public bool Required => false;

        public string[] Dependent => new string[] { };

        public StartupLoadType LoadType => StartupLoadType.Normal;

        public void AddClient(ServiceCollection serviceCollection, Config config, Assembly[] assemblies)
        {
            serviceCollection.AddSingleton<ReportClientMessenger>();
            serviceCollection.AddSingleton<ClientReportTransfer>();
        }

        public void AddServer(ServiceCollection serviceCollection, Config config, Assembly[] assemblies)
        {
            serviceCollection.AddSingleton<ReportServerMessenger>();
            serviceCollection.AddSingleton<ReportApiController>();
        }

        public void UseClient(ServiceProvider serviceProvider, Config config, Assembly[] assemblies)
        {
            Logger.Instance.Info($"start client report transfer");
            ClientReportTransfer report = serviceProvider.GetService<ClientReportTransfer>();
            report.LoadPlugins(assemblies);
        }

        public void UseServer(ServiceProvider serviceProvider, Config config, Assembly[] assemblies)
        {
        }
    }
}