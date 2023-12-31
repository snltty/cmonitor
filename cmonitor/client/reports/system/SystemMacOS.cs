﻿namespace cmonitor.client.reports.system
{
    public sealed class SystemMacOS : ISystem
    {
        public ReportDriveInfo[] GetAllDrives()
        {
            return Array.Empty<ReportDriveInfo>();
        }

        public double GetCpu()
        {
            return 0;
        }

        public double GetMemory()
        {
            return 0;
        }

        public Dictionary<string, SystemOptionKeyInfo> OptionKeys()
        {
            return new Dictionary<string, SystemOptionKeyInfo>();
        }
        public string OptionValues()
        {
            return string.Empty;
        }
        public void OptionUpdate(SystemOptionUpdateInfo registryUpdateInfo)
        {
        }

        public bool Password(PasswordInputInfo command)
        {
            return true;
        }
    }
}
