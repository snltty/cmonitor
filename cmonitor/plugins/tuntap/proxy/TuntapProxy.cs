﻿using cmonitor.client.config;
using cmonitor.config;
using cmonitor.plugins.relay;
using cmonitor.plugins.tuntap.vea;
using cmonitor.tunnel;
using cmonitor.tunnel.connection;
using cmonitor.tunnel.proxy;
using common.libs;
using common.libs.extends;
using common.libs.socks5;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace cmonitor.plugins.tuntap.proxy
{
    public sealed class TuntapProxy : TunnelProxy
    {
        private readonly TunnelTransfer tunnelTransfer;
        private readonly RelayTransfer relayTransfer;
        private readonly RunningConfig runningConfig;
        private readonly Config config;

        private IPEndPoint proxyEP;
        public override IPAddress UdpBindAdress { get; set; }

        private uint maskValue = NetworkHelper.MaskValue(24);
        private readonly ConcurrentDictionary<uint, string> dic = new ConcurrentDictionary<uint, string>();
        private readonly ConcurrentDictionary<string, ITunnelConnection> dicConnections = new ConcurrentDictionary<string, ITunnelConnection>();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> dicLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        public TuntapProxy(TunnelTransfer tunnelTransfer, RelayTransfer relayTransfer, RunningConfig runningConfig, Config config)
        {
            this.tunnelTransfer = tunnelTransfer;
            this.relayTransfer = relayTransfer;
            this.runningConfig = runningConfig;
            this.config = config;

            Start(0);
            proxyEP = new IPEndPoint(IPAddress.Any, LocalEndpoint.Port);
            Logger.Instance.Info($"start tuntap proxy, listen port : {LocalEndpoint}");


            tunnelTransfer.SetConnectedCallback("tuntap", OnConnected);
            relayTransfer.SetConnectedCallback("tuntap", OnConnected);
        }
        private void OnConnected(ITunnelConnection connection)
        {
            if (Logger.Instance.LoggerLevel <= LoggerTypes.DEBUG)
                Logger.Instance.Warning($"tuntap add connection {connection.GetHashCode()} {connection.ToJson()}");
            dicConnections.AddOrUpdate(connection.RemoteMachineId, connection, (a, b) => connection);
            BindConnectionReceive(connection);
        }

        public void SetIPs(List<TuntapVeaLanIPAddressList> ips)
        {
            dic.Clear();
            foreach (var item in ips)
            {
                foreach (var ip in item.IPS)
                {
                    dic.AddOrUpdate(ip.NetWork, item.MachineId, (a, b) => item.MachineId);
                }
            }
            UdpBindAdress = runningConfig.Data.Tuntap.ip;
        }
        public void SetIP(string machineId, uint ip)
        {
            dic.AddOrUpdate(ip, machineId, (a, b) => machineId);

            UdpBindAdress = runningConfig.Data.Tuntap.ip;
        }

        protected override async ValueTask<bool> ConnectTunnelConnection(AsyncUserToken token)
        {
            token.Proxy.TargetEP = null;
            token.Proxy.Rsv = (byte)Socks5EnumStep.Request;

            //步骤，request
            bool result = await ReceiveCommandData(token);
            if (result == false) return true;
            await token.Socket.SendAsync(new byte[] { 0x05, 0x00 });
            token.Proxy.Rsv = (byte)Socks5EnumStep.Command;
            token.Proxy.Data = Helper.EmptyArray;

            //步骤，command
            result = await ReceiveCommandData(token);
            if (result == false)
            {
                return true;
            }
            Socks5EnumRequestCommand command = (Socks5EnumRequestCommand)token.Proxy.Data.Span[1];

            //获取远端地址
            ReadOnlyMemory<byte> ipArray = Socks5Parser.GetRemoteEndPoint(token.Proxy.Data, out Socks5EnumAddressType addressType, out ushort port, out int index);
            //不支持域名 和 IPV6
            if (addressType == Socks5EnumAddressType.Domain || addressType == Socks5EnumAddressType.IPV6)
            {
                byte[] response1 = Socks5Parser.MakeConnectResponse(proxyEP, (byte)Socks5EnumResponseCommand.AddressNotAllow);
                await token.Socket.SendAsync(response1.AsMemory());
                return true;
            }


            token.Proxy.Data = token.Proxy.Data.Slice(index);
            token.TargetIP = BinaryPrimitives.ReadUInt32BigEndian(ipArray.Span);
            //是UDP中继，不做连接操作，等UDP数据过去的时候再绑定
            if (token.TargetIP == 0 || command == Socks5EnumRequestCommand.UdpAssociate)
            {
                await token.Socket.SendAsync(Socks5Parser.MakeConnectResponse(proxyEP, (byte)Socks5EnumResponseCommand.ConnecSuccess).AsMemory());
                return false;
            }

            token.Proxy.TargetEP = new IPEndPoint(new IPAddress(ipArray.Span), port);
            token.Connection = await ConnectTunnel(token.TargetIP);

            Socks5EnumResponseCommand resp = token.Connection != null && token.Connection.Connected ? Socks5EnumResponseCommand.ConnecSuccess : Socks5EnumResponseCommand.NetworkError;
            byte[] response = Socks5Parser.MakeConnectResponse(proxyEP, (byte)resp);
            await token.Socket.SendAsync(response.AsMemory());

            return true;
        }
        protected override async ValueTask ConnectTunnelConnection(AsyncUserUdpToken token)
        {
            ReadOnlyMemory<byte> ipArray = Socks5Parser.GetRemoteEndPoint(token.Proxy.Data, out Socks5EnumAddressType addressType, out ushort port, out int index);
            if (addressType == Socks5EnumAddressType.IPV6)
            {
                return;
            }

            token.Proxy.TargetEP = new IPEndPoint(new IPAddress(ipArray.Span), port);
            token.TargetIP = BinaryPrimitives.ReadUInt32BigEndian(ipArray.Span);

            //解析出udp包的数据部分
            token.Proxy.Data = Socks5Parser.GetUdpData(token.Proxy.Data);
            /*
            if (ipArray.GetIsBroadcastAddress())
            {
                token.Connections = new List<ITunnelConnection>();
                foreach (var item in dic.Values)
                {
                    ITunnelConnection cinnection = await ConnectTunnel(item);
                    if (cinnection != null)
                    {
                        token.Connections.Add(cinnection);
                    }
                }
            }
            else*/
            {
                token.Connection = await ConnectTunnel(token.TargetIP);
            }
        }
        protected override async ValueTask CheckTunnelConnection(AsyncUserToken token)
        {
            if (token.Connection == null || token.Connection.Connected == false)
            {
                token.Connection = await ConnectTunnel(token.TargetIP);
            }
        }

        protected override async ValueTask<bool> ConnectionReceiveUdp(AsyncUserTunnelToken token, AsyncUserUdpToken asyncUserUdpToken)
        {
            byte[] data = Socks5Parser.MakeUdpResponse(token.Proxy.TargetEP, token.Proxy.Data, out int length);
            try
            {
                await asyncUserUdpToken.SourceSocket.SendAsync(data.AsMemory(0, length), token.Proxy.SourceEP);
            }
            catch (Exception ex)
            {
                if (Logger.Instance.LoggerLevel <= LoggerTypes.DEBUG)
                {
                    Logger.Instance.Error(ex);
                }
            }
            finally
            {
                Socks5Parser.Return(data);
            }
            return true;
        }


        SemaphoreSlim slimGlobal = new SemaphoreSlim(1);
        private async ValueTask<ITunnelConnection> ConnectTunnel(uint ip)
        {
            uint network = ip & maskValue;
            if (dic.TryGetValue(ip, out string machineId) == false && dic.TryGetValue(network, out machineId) == false)
            {
                return null;
            }
            return await ConnectTunnel(machineId);
        }
        private async ValueTask<ITunnelConnection> ConnectTunnel(string machineId)
        {
            if (config.Data.Client.Id == machineId)
            {
                return null;
            }

            if (dicConnections.TryGetValue(machineId, out ITunnelConnection connection) && connection.Connected)
            {
                return connection;
            }

            await slimGlobal.WaitAsync();
            if (dicLocks.TryGetValue(machineId, out SemaphoreSlim slim) == false)
            {
                slim = new SemaphoreSlim(1);
                dicLocks.TryAdd(machineId, slim);
            }
            slimGlobal.Release();

            await slim.WaitAsync();

            try
            {

                if (dicConnections.TryGetValue(machineId, out connection) && connection.Connected)
                {
                    return connection;
                }

                if (Logger.Instance.LoggerLevel <= LoggerTypes.DEBUG) Logger.Instance.Debug($"tuntap tunnel to {machineId}");

                connection = await tunnelTransfer.ConnectAsync(machineId, "tuntap");
                if (connection != null)
                {
                    if (Logger.Instance.LoggerLevel <= LoggerTypes.DEBUG) Logger.Instance.Debug($"tuntap tunnel success,{connection.ToString()}");
                }
                if (connection == null)
                {
                    if (Logger.Instance.LoggerLevel <= LoggerTypes.DEBUG) Logger.Instance.Debug($"tuntap relay to {machineId}");

                    connection = await relayTransfer.ConnectAsync(config.Data.Client.Id, machineId, "tuntap");
                    if (connection != null)
                    {
                        if (Logger.Instance.LoggerLevel <= LoggerTypes.DEBUG) Logger.Instance.Debug($"tuntap relay success,{connection.ToString()}");
                    }
                }
                if (connection != null)
                {
                    dicConnections.AddOrUpdate(machineId, connection, (a, b) => connection);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                slim.Release();
            }
            return connection;
        }


        private async Task<bool> ReceiveCommandData(AsyncUserToken token)
        {
            int totalLength = token.Proxy.Data.Length;
            EnumProxyValidateDataResult validate = ValidateData(token.Proxy);
            if ((validate & EnumProxyValidateDataResult.TooShort) == EnumProxyValidateDataResult.TooShort)
            {
                //太短
                while ((validate & EnumProxyValidateDataResult.TooShort) == EnumProxyValidateDataResult.TooShort)
                {
                    totalLength += await token.Socket.ReceiveAsync(token.Saea.Buffer.AsMemory(token.Saea.Offset + totalLength), SocketFlags.None);
                    token.Proxy.Data = token.Saea.Buffer.AsMemory(token.Saea.Offset, totalLength);
                    validate = ValidateData(token.Proxy);
                }
            }

            //不短，又不相等，直接关闭连接
            if ((validate & EnumProxyValidateDataResult.Equal) != EnumProxyValidateDataResult.Equal)
            {
                return false;
            }
            return true;
        }
        public EnumProxyValidateDataResult ValidateData(ProxyInfo info)
        {
            return (Socks5EnumStep)info.Rsv switch
            {
                Socks5EnumStep.Request => Socks5Parser.ValidateRequestData(info.Data),
                Socks5EnumStep.Command => Socks5Parser.ValidateCommandData(info.Data),
                Socks5EnumStep.Auth => Socks5Parser.ValidateAuthData(info.Data, Socks5EnumAuthType.Password),
                Socks5EnumStep.Forward => EnumProxyValidateDataResult.Equal,
                Socks5EnumStep.ForwardUdp => EnumProxyValidateDataResult.Equal,
                _ => EnumProxyValidateDataResult.Equal
            };
        }
    }
}
