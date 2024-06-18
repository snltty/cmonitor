﻿using common.libs.extends;
using MemoryPack;
using System.Net;

namespace cmonitor.serializes
{
    /// <summary>
    ///  MemoryPack 的 IPEndPoint序列化扩展
    /// </summary>
    public sealed class IPEndPointFormatter : MemoryPackFormatter<IPEndPoint>
    {
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref IPEndPoint value)
        {
            if (value == null)
            {
                writer.WriteNullCollectionHeader();
                return;
            }

            //最多 IPV6 16byte + 端口 2byte + 头部 4byte
            Memory<byte> memory = new byte[22];
            Span<byte> span = memory.Span;
            int index = 1;

            value.Address.TryWriteBytes(span.Slice(index), out int bytesWritten);
            index += bytesWritten;
            span[0] = (byte)bytesWritten;

            ushort port = (ushort)value.Port;
            port.ToBytes(memory.Slice(index));
            index += 2;

            writer.WriteCollectionHeader(index + 4);
            writer.WriteSpan(span.Slice(0, index));
        }

        public override void Deserialize(ref MemoryPackReader reader, scoped ref IPEndPoint value)
        {
            if (!reader.TryReadCollectionHeader(out int len))
            {
                value = null;
                return;
            }


            Span<byte> span = Array.Empty<byte>();
            reader.ReadSpan(ref span);

            int length = span[0];
            value = new IPEndPoint(new IPAddress(span.Slice(0 + 1, length)), span.Slice(0 + 1 + length).ToUInt16());
        }
    }



}
