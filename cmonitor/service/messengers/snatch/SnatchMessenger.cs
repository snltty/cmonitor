﻿using cmonitor.client.reports.snatch;
using cmonitor.service.messengers.sign;

namespace cmonitor.service.messengers.snatch
{
    public sealed class SnatchMessenger : IMessenger
    {
        private readonly SnatchReport snatchReport;
        private readonly ISnatachCaching snatachCaching;
        private readonly SignCaching signCaching;
        private readonly MessengerSender messengerSender;

        public SnatchMessenger(SnatchReport snatchReport, ISnatachCaching snatachCaching, SignCaching signCaching, MessengerSender messengerSender)
        {
            this.snatchReport = snatchReport;
            this.snatachCaching = snatachCaching;
            this.signCaching = signCaching;
            this.messengerSender = messengerSender;
        }

        [MessengerId((ushort)SnatchMessengerIds.AddQuestion)]
        public void AddQuestion(IConnection connection)
        {
            SnatchQuestionInfo question = SnatchQuestionInfo.DeBytes(connection.ReceiveRequestWrap.Payload);
            snatchReport.Add(question);
        }
        [MessengerId((ushort)SnatchMessengerIds.UpdateQuestion)]
        public void UpdateQuestion(IConnection connection)
        {
            SnatchQuestionInfo question = SnatchQuestionInfo.DeBytes(connection.ReceiveRequestWrap.Payload);
            snatchReport.Update(question);
        }
        [MessengerId((ushort)SnatchMessengerIds.RemoveQuestion)]
        public void RemoveQuestion(IConnection connection)
        {
            snatchReport.Remove();
        }

        [MessengerId((ushort)SnatchMessengerIds.UpdateAnswer)]
        public void UpdateAnswer(IConnection connection)
        {
            SnatchAnswerInfo answerInfo = SnatchAnswerInfo.DeBytes(connection.ReceiveRequestWrap.Payload);
            snatachCaching.Update(connection.Name, answerInfo);

            Task.Run(async () =>
            {
                if (snatachCaching.Get(answerInfo.UserName, connection.Name, out SnatchAnswerInfo info))
                {
                    byte[] bytes = info.Question.ToBytes();
                    SnatchAnswerInfo[] answers = snatachCaching.Get(info);
                    foreach (var item in answers)
                    {
                        if (signCaching.Get(item.MachineName, out SignCacheInfo cache))
                        {
                            await messengerSender.SendOnly(new MessageRequestWrap
                            {
                                Connection = cache.Connection,
                                MessengerId = (ushort)SnatchMessengerIds.UpdateQuestion,
                                Payload = bytes
                            });
                        }
                    }
                }
            });
        }
    }

}
