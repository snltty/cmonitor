﻿using cmonitor.config;
using cmonitor.startup;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace cmonitor.plugins.viewer
{
    public sealed class TuntapStartup : IStartup
    {
        public StartupLevel Level => StartupLevel.Normal;

        public void AddClient(ServiceCollection serviceCollection, Config config, Assembly[] assemblies)
        {

        }

        public void AddServer(ServiceCollection serviceCollection, Config config, Assembly[] assemblies)
        {
        }

        public void UseClient(ServiceProvider serviceProvider, Config config, Assembly[] assemblies)
        {
        }

        public void UseServer(ServiceProvider serviceProvider, Config config, Assembly[] assemblies)
        {
        }
    }
}
