﻿using cmonitor.api;
using cmonitor.client.reports.screen;
using cmonitor.service.messengers.sign;
using MemoryPack;

namespace cmonitor.service.messengers.screen
{
    public sealed class ScreenMessenger : IMessenger
    {
        private readonly ScreenReport screenReport;
        private readonly IClientServer clientServer;
        private readonly Config config;
        private readonly SignCaching signCaching;
        private readonly ScreenShare screenShare;


        public ScreenMessenger(ScreenReport screenReport, IClientServer clientServer, Config config, SignCaching signCaching, ScreenShare screenShare)
        {
            this.screenReport = screenReport;
            this.clientServer = clientServer;
            this.config = config;
            this.signCaching = signCaching;
            this.screenShare = screenShare;
        }

        [MessengerId((ushort)ScreenMessengerIds.CaptureFull)]
        public void CaptureFull(IConnection connection)
        {
            ScreenReportFullType reportType = ScreenReportFullType.Trim;
            if (connection.ReceiveRequestWrap.Payload.Length > 0)
            {
                reportType = (ScreenReportFullType)connection.ReceiveRequestWrap.Payload.Span[0];
            }
            screenReport.SetCaptureFull(reportType);
        }

        [MessengerId((ushort)ScreenMessengerIds.CaptureFullReport)]
        public async Task CaptureFullReport(IConnection connection)
        {
            bool shared = await screenShare.ShareData(connection.Name, connection.ReceiveRequestWrap.Payload);
            if (shared) return;

            if (signCaching.Get(connection.Name, out SignCacheInfo cache))
            {
                if (cache.Version == config.Version)
                {
                    clientServer.Notify("/notify/report/screen/full", connection.Name, connection.ReceiveRequestWrap.Payload);
                }
                else
                {
                    string base64 = MemoryPackSerializer.Deserialize<string>(connection.ReceiveRequestWrap.Payload.Span);
                    clientServer.Notify("/notify/report/screen/full", new { connection.Name, Img = base64 });
                }
            }
        }

        [MessengerId((ushort)ScreenMessengerIds.CaptureClip)]
        public void CaptureClip(IConnection connection)
        {
            screenReport.SetCaptureClip(MemoryPackSerializer.Deserialize<ScreenClipInfo>(connection.ReceiveRequestWrap.Payload.Span));
        }

        [MessengerId((ushort)ScreenMessengerIds.CaptureRegion)]
        public void CaptureRegion(IConnection connection)
        {
            screenReport.SetCaptureRegion();
        }

        [MessengerId((ushort)ScreenMessengerIds.CaptureRegionReport)]
        public void CaptureRegionReport(IConnection connection)
        {
            clientServer.Notify("/notify/report/screen/region", connection.Name, connection.ReceiveRequestWrap.Payload);
        }

        [MessengerId((ushort)ScreenMessengerIds.CaptureRectangles)]
        public void CaptureRectangles(IConnection connection)
        {
            Rectangle[] rectangles = MemoryPackSerializer.Deserialize<Rectangle[]>(connection.ReceiveRequestWrap.Payload.Span);
            clientServer.Notify("/notify/report/screen/rectangles", new { Name = connection.Name, Rectangles = rectangles });
        }


        [MessengerId((ushort)ScreenMessengerIds.DisplayState)]
        public void DisplayState(IConnection connection)
        {
            if (connection.ReceiveRequestWrap.Payload.Length == 1)
            {
                byte state = connection.ReceiveRequestWrap.Payload.Span[0];
                screenReport.SetDisplayState(state == 1);
            }
        }


        [MessengerId((ushort)ScreenMessengerIds.ShareData)]
        public void ShareData(IConnection connection)
        {
            screenShare.SetData(connection.ReceiveRequestWrap.Payload);
        }

        [MessengerId((ushort)ScreenMessengerIds.ShareStart)]
        public async Task ShareStart(IConnection connection)
        {
            string[] names = Array.Empty<string>();
            if (connection.ReceiveRequestWrap.Payload.Length > 0)
            {
                names = MemoryPackSerializer.Deserialize<string[]>(connection.ReceiveRequestWrap.Payload.Span);
            }
            await screenShare.Start(connection.Name, names);
        }

        [MessengerId((ushort)ScreenMessengerIds.ShareClose)]
        public async Task ShareClose(IConnection connection)
        {
            await screenShare.Close(connection.Name);
        }
    }

}
