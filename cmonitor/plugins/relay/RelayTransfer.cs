﻿using cmonitor.client.tunnel;
using cmonitor.config;
using cmonitor.plugins.relay.transport;
using common.libs;
using common.libs.extends;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Reflection;

namespace cmonitor.plugins.relay
{
    public sealed class RelayTransfer
    {
        private List<ITransport> transports;

        private readonly Config config;
        private readonly ServiceProvider serviceProvider;

        private Dictionary<string, List<Action<ITunnelConnection>>> OnConnected { get; } = new Dictionary<string, List<Action<ITunnelConnection>>>();

        public RelayTransfer(Config config, ServiceProvider serviceProvider)
        {
            this.config = config;
            this.serviceProvider = serviceProvider;
        }

        public void Load(Assembly[] assembs)
        {
            IEnumerable<Type> types = ReflectionHelper.GetInterfaceSchieves(assembs, typeof(ITransport));
            transports = types.Select(c => (ITransport)serviceProvider.GetService(c)).Where(c => c != null).Where(c => string.IsNullOrWhiteSpace(c.Name) == false).ToList();

            Logger.Instance.Warning($"load relay transport:{string.Join(",", transports.Select(c => c.Name))}");
        }

        public void SetConnectedCallback(string transactionId, Action<ITunnelConnection> callback)
        {
            if (OnConnected.TryGetValue(transactionId, out List<Action<ITunnelConnection>> callbacks) == false)
            {
                callbacks = new List<Action<ITunnelConnection>>();
                OnConnected[transactionId] = callbacks;
            }
            callbacks.Add(callback);
        }
        public void RemoveConnectedCallback(string transactionId, Action<ITunnelConnection> callback)
        {
            if (OnConnected.TryGetValue(transactionId, out List<Action<ITunnelConnection>> callbacks))
            {
                callbacks.Remove(callback);
            }
        }

        public async Task<ITunnelConnection> ConnectAsync(string remoteMachineName, string transactionId)
        {
            IEnumerable<ITransport> _transports = transports.OrderBy(c => c.Type);
            foreach (RelayCompactInfo item in config.Data.Client.Relay.Servers.Where(c => c.Disabled == false))
            {
                ITransport transport = _transports.FirstOrDefault(c => c.Type == item.Type);
                if (transport == null)
                {
                    continue;
                }

                try
                {
                    IPEndPoint server = NetworkHelper.GetEndPoint(item.Host, 3478);
                    RelayInfo relayInfo = new RelayInfo
                    {
                        FlowingId = 0,
                        RemoteMachineName = remoteMachineName,
                        SecretKey = item.SecretKey,
                        Server = server,
                        TransactionId = transactionId,
                        TransportName = transport.Name
                    };
                    if (Logger.Instance.LoggerLevel <= LoggerTypes.DEBUG)
                        Logger.Instance.Debug($"relay to {relayInfo.RemoteMachineName} {relayInfo.ToJson()}");
                    ITunnelConnection connection = await transport.RelayAsync(relayInfo);
                    if (connection != null)
                    {
                        return connection;
                    }
                    else
                    {
                        Logger.Instance.Error($"relay to {relayInfo.RemoteMachineName} fail,{relayInfo.ToJson()}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error(ex);
                }
            }
            return null;
        }
        public async Task<bool> OnBeginAsync(RelayInfo relayInfo)
        {
            ITransport _transports = transports.FirstOrDefault(c => c.Name == relayInfo.TransportName);
            if (_transports != null)
            {
                ITunnelConnection connection = await _transports.OnBeginAsync(relayInfo);
                if (connection != null)
                {
                    if (OnConnected.TryGetValue(connection.TransactionId, out List<Action<ITunnelConnection>> callbacks))
                    {
                        foreach (var item in callbacks)
                        {
                            item(connection);
                        }
                    }
                    return true;
                }
            }
            return false;
        }

    }
}
