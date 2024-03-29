﻿using common.libs;
using common.libs.winapis;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace cmonitor.client.reports.active
{
    public sealed class ActiveWindowWindows : IActiveWindow
    {
        public ActiveWindowWindows(Config config)
        {
            if (config.IsCLient)
            {
                CreateKey();
                DisallowRunClear();
                DisallowRun(false);
                Task.Run(() =>
                {
                    CommandHelper.Windows(string.Empty, new string[] { "gpupdate /force" });
                });
                InitDriver();

                AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                {
                    CommandHelper.Windows(string.Empty, new string[] { "sc stop cmonitor.killer & sc delete cmonitor.killer" }, true);
                };
            }
        }

        private void CreateKey()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer", true);
                    RegistryKey disallowRun = key.OpenSubKey("DisallowRun");
                    if (disallowRun == null)
                    {
                        key.CreateSubKey("DisallowRun");
                    }
                }
            }
            catch (Exception)
            {
            }
        }
        private string[] disallowNames = Array.Empty<string>();
        public void DisallowRun(string[] names)
        {
            DisallowRun(false);
            DisallowRunClear();
            disallowNames = names;

            Task.Run(() =>
            {
                if (names.Length > 0)
                {
                    DisallowRun(true);
                    DisallowRunFileNames(names);
                }
                CommandHelper.Windows(string.Empty, new string[] { "gpupdate /force" });
            });
        }
        private void DisallowRunClear()
        {
            try
            {

                if (OperatingSystem.IsWindows())
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer\\DisallowRun", true);
                    if (key != null)
                    {
                        string[] names = key.GetValueNames();
                        if (names != null)
                        {
                            foreach (string name in names)
                            {
                                key.DeleteValue(name, false);
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"application disallow clear {ex}");
            }
        }
        private void DisallowRun(bool value)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer", true);
                    if (key != null)
                    {
                        key.SetValue("DisallowRun", value ? 1 : 0, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
        private void DisallowRunFileNames(string[] filenames)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {

                    RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer\\DisallowRun", true);
                    if (key != null)
                    {
                        foreach (string filename in filenames)
                        {
                            key.SetValue(filename, filename, RegistryValueKind.String);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"application disallow {string.Join(",", filenames)} {ex}");
            }
        }
        private bool Disallow(ActiveWindowInfo window)
        {
            if (disallowNames.Length > 0)
            {
                try
                {
                    ReadOnlySpan<char> filenameSpan = window.FileName.AsSpan();
                    uint pid = window.Pid;
                    foreach (string item in disallowNames)
                    {
                        ReadOnlySpan<char> nameSpan = item.AsSpan();
                        bool result = item == window.Title
                            || (filenameSpan.Length >= nameSpan.Length && filenameSpan.Slice(filenameSpan.Length - nameSpan.Length, nameSpan.Length).SequenceEqual(nameSpan))
                            || (item.StartsWith('/') && item.EndsWith('/') && Regex.IsMatch(window.Title, item.Trim('/')));
                        if (result)
                        {
                            Task.Run(() =>
                            {
                                CommandHelper.Windows(string.Empty, new string[] { $"taskkill /f /pid {pid}"});
                                ProcessKiller((uint)pid);
                            });
                        }
                    }
                }
                catch (Exception)
                {
                }
                return true;
            }
            return false;
        }

        const int nChars = 256;
        private StringBuilder buff = new StringBuilder(nChars);
        public ActiveWindowInfo GetActiveWindow()
        {
            ActiveWindowInfo activeWindowInfo = new ActiveWindowInfo();
            IntPtr handle = User32.GetForegroundWindow();
            User32.GetWindowThreadProcessId(handle, out uint id);
            if (User32.GetWindowText(handle, buff, nChars) > 0)
            {
                Process p = Process.GetProcessById((int)id);
                string desc = string.Empty;
                string filename = string.Empty;

                try
                {
                    ProcessModule main = p.MainModule;
                    if (main != null)
                    {
                        filename = main.FileName;
                        desc = main.FileVersionInfo.FileDescription;
                    }
                }

                catch (Exception)
                {
                }

                activeWindowInfo.Title = buff.ToString();
                activeWindowInfo.FileName = Path.GetFileName(filename);
                activeWindowInfo.Desc = desc;
                activeWindowInfo.Pid = id;

                Disallow(activeWindowInfo);
            }
            else
            {
                activeWindowInfo.Title = string.Empty;
                activeWindowInfo.FileName = string.Empty;
                activeWindowInfo.Desc = string.Empty;
                activeWindowInfo.Pid = 0;
            }
            return activeWindowInfo;
        }
        public int GetWindowCount()
        {
            int length = 0;
            User32.EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                try
                {
                    if (User32.IsWindowVisible(hWnd) && User32.GetWindowTextLength(hWnd) > 0)
                    {
                        length++;
                    }
                }
                catch (Exception)
                {
                }

                return true;
            }, IntPtr.Zero);
            return length;
        }
        public Dictionary<uint, string> GetWindows()
        {
            Dictionary<uint, string> dic = new Dictionary<uint, string>();
            StringBuilder lpString = new StringBuilder(256);
            User32.EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                try
                {
                    if (User32.IsWindowVisible(hWnd) && User32.GetWindowTextLength(hWnd) > 0)
                    {
                        User32.GetWindowText(hWnd, lpString, 256);
                        User32.GetWindowThreadProcessId(hWnd, out uint id);

                        dic[id] = lpString.ToString();
                    }
                }
                catch (Exception)
                {
                }

                return true;
            }, IntPtr.Zero);

            /*
            try
            {
                var processs = Process.GetProcesses();
                foreach (var item in processs)
                {
                    try
                    {
                        if(string.IsNullOrWhiteSpace(item.MainModule.FileVersionInfo.FileDescription) == false)
                        {
                            dic[(uint)item.Id] = item.MainModule.FileVersionInfo.FileDescription;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
            */
            return dic;
        }

        private void InitDriver()
        {

            try
            {
                //#if RELEASE
                LoadDriver("cmonitor.killer", Path.GetFullPath(Path.Join("./", "killer.sys")));
                //#endif
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex);
            }
        }
        public void Kill(int pid)
        {
            try
            {
                int res = ProcessKiller((uint)pid);
                if (res == 0)
                {

                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex);
            }
        }


        [DllImport("cmonitor.killer.dll")]
        public static extern int LoadDriver(string serviceName, string driverPath);

        [DllImport("cmonitor.killer.dll")]
        public static extern int ProcessKiller(uint pid);
    }
}
