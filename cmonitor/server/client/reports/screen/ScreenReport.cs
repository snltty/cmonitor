﻿using cmonitor.server.service;
using common.libs;
using cmonitor.server.service.messengers.screen;
using MemoryPack;
using System.Diagnostics;

namespace cmonitor.server.client.reports.screen
{
    public sealed class ScreenReport : IReport
    {

        public string Name => "Screen";
        private readonly ClientSignInState clientSignInState;
        private readonly MessengerSender messengerSender;
        private readonly Config config;

        private ScreenReportInfo report = new ScreenReportInfo();
        private uint lastInput;
        private readonly DxgiDesktop dxgiDesktop;
        private readonly GdiDesktop gdiDesktop;

        public ScreenReport(ClientSignInState clientSignInState, MessengerSender messengerSender, Config config)
        {
            this.clientSignInState = clientSignInState;
            this.messengerSender = messengerSender;
            this.config = config;
            if (config.IsCLient)
            {
                ScreenCaptureTask();
                WinApi.InitLastInputInfo();
                dxgiDesktop = new DxgiDesktop(0);
                gdiDesktop = new GdiDesktop();
            }

        }
        public object GetReports(ReportType reportType)
        {
            report.LT = WinApi.GetLastInputInfo();
            if (report.LT < lastInput || report.LT - lastInput > 1000)
            {
                lastInput = report.LT;
                return report;
            }
            return null;
        }


        private ScreenReportType screenReportType = ScreenReportType.None;
        private long ticks = 0;
        public void Full()
        {
            ticks = DateTime.UtcNow.Ticks;
            screenReportType = ScreenReportType.Full;
        }
        public void Clip(ScreenClipInfo screenClipInfo)
        {
            ticks = DateTime.UtcNow.Ticks;
            screenReportType = ScreenReportType.Full;
            gdiDesktop.Clip(screenClipInfo);
        }
        public void Region()
        {
            ticks = DateTime.UtcNow.Ticks;
            screenReportType = ScreenReportType.Region;
        }

        private void ScreenCaptureTask()
        {
            if (OperatingSystem.IsWindows() == false)
            {
                return;
            }
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    int delayms = 0;
                    if (clientSignInState.Connected == true && ((DateTime.UtcNow.Ticks - ticks) / TimeSpan.TicksPerMillisecond < 1000))
                    {
                        try
                        {
                            long start = DateTime.UtcNow.Ticks;
                            await SendScreenCapture();
                            delayms = (int)((DateTime.UtcNow.Ticks - start) / TimeSpan.TicksPerMillisecond);
                        }
                        catch (Exception ex)
                        {
                            if (Logger.Instance.LoggerLevel <= LoggerTypes.DEBUG)
                                Logger.Instance.Error(ex);
                        }
                    }
                    if (delayms < config.ScreenDelay)
                    {
                        Thread.Sleep(config.ScreenDelay - delayms);
                    }
                    else
                    {
                        Thread.Sleep(config.ScreenDelay);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }
        private async Task SendScreenCapture()
        {
            DesktopFrame frame = null;
            if (gdiDesktop.IsClip()/*|| gdiDesktop.IsLockScreen()*/)
            {
                frame = gdiDesktop.GetLatestFrame(config.ScreenScale);
            }
            else if (screenReportType == ScreenReportType.Full)
            {
                //var sw = new Stopwatch();
                //sw.Start();
                frame = dxgiDesktop.GetLatestFullFrame(config.ScreenScale);
                //sw.Stop();
                //Console.WriteLine(sw.ElapsedMilliseconds);
            }
            else if (screenReportType == ScreenReportType.Region)
            {
                frame = dxgiDesktop.GetLatestRegionFrame(config.ScreenScale);
            }

            if (frame != null)
            {
                if (frame.FullImage.Length > 0)
                {
                    await messengerSender.SendOnly(new MessageRequestWrap
                    {
                        Connection = clientSignInState.Connection,
                        MessengerId = (ushort)ScreenMessengerIds.FullReport,
                        Payload = frame.FullImage,
                    });
                }
                else if (frame.RegionImage.Length > 0)
                {
                    await messengerSender.SendOnly(new MessageRequestWrap
                    {
                        Connection = clientSignInState.Connection,
                        MessengerId = (ushort)ScreenMessengerIds.RegionReport,
                        Payload = frame.RegionImage,
                    });
                }
                if (frame.UpdatedRegions.Length > 0)
                {
                    await messengerSender.SendOnly(new MessageRequestWrap
                    {
                        Connection = clientSignInState.Connection,
                        MessengerId = (ushort)ScreenMessengerIds.Rectangles,
                        Payload = MemoryPackSerializer.Serialize(frame.UpdatedRegions),
                    });
                }
            }

        }
    }

    public sealed class ScreenReportInfo
    {
        public uint LT { get; set; }
    }

    public enum ScreenReportType : byte
    {
        None = 0,
        Full = 1,
        Region = 2
    };

    [MemoryPackable]
    public sealed partial class ScreenClipInfo
    {
        public int X { get; set; }
        public int Y { get; set; }
        public float Scale { get; set; }

    }
}
