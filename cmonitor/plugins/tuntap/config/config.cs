﻿using cmonitor.plugins.tuntap.config;
using System.Net;

namespace cmonitor.plugins.tuntap.config
{
    public sealed class TuntapConfigInfo
    {
        public IPAddress IP { get; set; } = IPAddress.Any;
        public IPAddress[] LanIPs { get; set; } = Array.Empty<IPAddress>();
        public bool Running { get; set; }
    }
}


namespace cmonitor.config
{
    public sealed partial class ConfigClientInfo
    {
        public TuntapConfigInfo Tuntap { get; set; } = new TuntapConfigInfo();
    }
}