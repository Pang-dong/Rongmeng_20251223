using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Rongmeng_20251223.LH.MessageType;

namespace Rongmeng_20251223.Interface.Model
{
    public  class FactoryDocommand : Docomand,IDocommand
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public FactoryDocommand() { }
        /// <summary>
        /// 2. 仅命令字构造 (无参数)
        /// </summary>
        /// <param name="packetType">消息类型 (如 0x02)</param>
        /// <param name="commandType">命令字 (如 0x0020)</param>
        public FactoryDocommand(MessageTypes packetType, CommandType commandType)
        {
            InitHeader((uint)packetType, (ushort)commandType);
            // Payload 留空 (Array.Empty<byte>)
            this.PlayLoad = Array.Empty<byte>();
            UpdatePayloadLength();
        }
        /// <summary>
        /// 3. 字典参数构造 (序列化为 JSON)
        /// </summary>
        /// <param name="packetType">消息类型</param>
        /// <param name="commandType">命令字</param>
        /// <param name="nodes">参数字典</param>
        public FactoryDocommand(byte packetType, ushort commandType, Dictionary<string, object> nodes)
        {
            InitHeader(packetType, commandType);

            if (nodes != null && nodes.Count > 0)
            {
                // 将字典序列化为 JSON 字符串，再转为字节
                string json = JsonConvert.SerializeObject(nodes);
                this.PlayLoad = Encoding.UTF8.GetBytes(json);
            }
            else
            {
                this.PlayLoad = Array.Empty<byte>();
            }
            UpdatePayloadLength();
        }
        /// <summary>
        /// 4. 字符串参数构造 (直接发送字符串)
        /// </summary>
        /// <param name="packetType">消息类型</param>
        /// <param name="commandType">命令字</param>
        /// <param name="data">字符串数据</param>
        public FactoryDocommand(MessageTypes packetType, CommandType commandType, string data)
        {
            InitHeader((uint)packetType, (ushort)commandType);

            if (!string.IsNullOrEmpty(data))
            {
                this.PlayLoad = Encoding.UTF8.GetBytes(data);
            }
            else
            {
                this.PlayLoad = Array.Empty<byte>();
            }
            UpdatePayloadLength();
        }
        /// <summary>
        /// 5. 单个 Int32 参数构造
        /// </summary>
        /// <param name="packetType">消息类型</param>
        /// <param name="commandType">命令字</param>
        /// <param name="value">Int32 数值</param>
        public FactoryDocommand(MessageTypes packetType, CommandType commandType, int value)
        {
            // 初始化头部
            InitHeader((uint)packetType, (ushort)commandType);

            // 将 int 转换为 4 字节的 byte 数组
            this.PlayLoad = BitConverter.GetBytes(value);

            // 更新长度字段
            UpdatePayloadLength();
        }
        private void InitHeader(uint  packetType, ushort commandType)
        {
            // 设置 MsgType (4字节, 小端序)
            this.PacketType = BitConverter.GetBytes(packetType);
            // 设置 CmdType (2字节, 小端序)
            this.CommandType = BitConverter.GetBytes(commandType);
        }
        private void UpdatePayloadLength()
        {
            int totalLen = 8 + 2 + PlayLoad.Length;
            this.PlayloadLenth = BitConverter.GetBytes((uint)totalLen);
        }
        public byte[] ToBuffer()
        {
            int cmdPacketSize = 2 + PlayLoad.Length;
            byte[] cmdPacket = new byte[cmdPacketSize];

            // 写入 CmdType (2字节)
            Array.Copy(CommandType, 0, cmdPacket, 0, 2);
            // 写入 Payload (如果有)
            if (PlayLoad.Length > 0)
            {
                Array.Copy(PlayLoad, 0, cmdPacket, 2, PlayLoad.Length);
            }
            byte[] msgPacketLenBytes = BitConverter.GetBytes((uint)cmdPacketSize);

            int msgPacketSize = 4 + 4 + cmdPacketSize;
            byte[] msgPacket = new byte[msgPacketSize];

            // 写入 MsgType (4字节)
            Array.Copy(PacketType, 0, msgPacket, 0, 4);
            // 写入 DataLen (4字节)
            Array.Copy(msgPacketLenBytes, 0, msgPacket, 4, 4);
            // 写入 CmdPacket
            Array.Copy(cmdPacket, 0, msgPacket, 8, cmdPacketSize);

            // 3. 构建 MessageHead (16字节) + MsgPacket
            byte[] buffer = new byte[16 + msgPacketSize];

            // Header 0-11
            buffer[0] = MagicNum[0]; buffer[1] = MagicNum[1];
            buffer[2] = Version;
            buffer[3] = DeviceID;
            buffer[4] = PacketCount;
            buffer[5] = PackID;
            Array.Copy(Reserved, 0, buffer, 6, 4);
            buffer[10] = Staus;

            // 计算 BCC (对 MsgPacket 进行校验)
            byte bcc = 0;
            foreach (byte b in msgPacket) bcc ^= b;
            buffer[11] = bcc;

            // Header 12-15 (DataLength = MsgPacket的总长度)
            byte[] totalLenBytes = BitConverter.GetBytes((uint)msgPacketSize);
            Array.Copy(totalLenBytes, 0, buffer, 12, 4);

            // 4. 拼接 MsgPacket
            Array.Copy(msgPacket, 0, buffer, 16, msgPacketSize);

            return buffer;
        }
        public IDocommand FromBuffer(byte[] buffer)
        {
            FactoryDocommand cmd = new FactoryDocommand();

            // 如果 buffer 为空或长度不足最小头部(16字节)，直接返回空对象
            if (buffer == null || buffer.Length < 16)
            {
                cmd.IsFullyParsed = false;
                return cmd;
            }

            int startIndex = 0;
            cmd.MagicNum = new byte[2];
            Array.Copy(buffer, startIndex, cmd.MagicNum, 0, 2);
            startIndex += 2;

            // 校验魔数 'U', 'I' (0x55, 0x49)
            if (cmd.MagicNum[0] != (byte)'U' || cmd.MagicNum[1] != (byte)'I')
            {
                cmd.IsFullyParsed = false;
                return cmd;
            }

            // 1.2 版本号 (Version) - 1字节
            cmd.Version = buffer[startIndex];
            startIndex += 1;

            // 1.3 设备ID (DeviceId) - 1字节
            cmd.DeviceID = buffer[startIndex];
            startIndex += 1;

            // 1.4 包总数 (PacketCount) - 1字节
            cmd.PacketCount = buffer[startIndex];
            startIndex += 1;

            // 1.5 当前包ID (PacketId) - 1字节
            cmd.PackID = buffer[startIndex];
            startIndex += 1;

            // 1.6 保留字段 (Reserved) - 4字节
            cmd.Reserved = new byte[4];
            Array.Copy(buffer, startIndex, cmd.Reserved, 0, 4);
            startIndex += 4;

            // 1.7 状态 (Status) - 1字节
            cmd.Staus = buffer[startIndex];
            startIndex += 1;

            // 1.8 校验和 (DataBcc) - 1字节
            cmd.DataBcc = buffer[startIndex];
            startIndex += 1;

            // 1.9 第一层数据长度 (DataLength) - 4字节
            // 这个长度 = MsgPacket头(8) + ImagePacket头(32) + 实际数据
            byte[] layer1LenBytes = new byte[4];
            Array.Copy(buffer, startIndex, layer1LenBytes, 0, 4);
            startIndex += 4;

            // 转换为整数以便后续判断
            uint layer1DataLen = BitConverter.ToUInt32(layer1LenBytes, 0);

            // 校验完整性：如果 Buffer 剩余数据不够 Layer1 描述的长度，说明是半包
            if (buffer.Length < startIndex + layer1DataLen)
            {
                cmd.IsFullyParsed = false;
                return cmd;
            }
            cmd.PacketType = new byte[4];
            Array.Copy(buffer, startIndex, cmd.PacketType, 0, 4);
            startIndex += 4;

            uint msgType = BitConverter.ToUInt32(cmd.PacketType, 0);
            byte[] layer2LenBytes = new byte[4];
            Array.Copy(buffer, startIndex, layer2LenBytes, 0, 4);
            startIndex += 4;
            cmd.PlayloadLenth = layer2LenBytes;


            if (msgType == 0x00) // MessageType.ImageData
            {
                int remainingLen = (int)layer1DataLen - (8 + 28);

                if (remainingLen > 0)
                {
                    cmd.PlayLoad = new byte[remainingLen];
                    Array.Copy(buffer, startIndex, cmd.PlayLoad, 0, remainingLen);
                    startIndex += remainingLen;
                }
            }
            else if (msgType == 0x02) // MessageType.Command
            {

                // 3.1 命令类型 (CommandType) - 2字节
                cmd.CommandType = new byte[2];
                Array.Copy(buffer, startIndex, cmd.CommandType, 0, 2);
                startIndex += 2;
                int paramLen = (int)layer1DataLen - (8 + 2);

                if (paramLen > 0)
                {
                    cmd.PlayLoad = new byte[paramLen];
                    Array.Copy(buffer, startIndex, cmd.PlayLoad, 0, paramLen);
                    startIndex += paramLen;
                }
            }
            else if (msgType == 0xF0) // MessageType.Response
            {
                cmd.IsResponse = true;
                cmd.CommandType = new byte[2];
                Array.Copy(buffer, startIndex, cmd.CommandType, 0, 2);
                startIndex += 2;
                cmd.ResponseStatus = buffer[startIndex];
                startIndex += 1;
                int paramLen = (int)layer1DataLen - (8 + 3);
                if (paramLen > 0)
                {
                    cmd.ResponseParameters = new byte[paramLen];
                    Array.Copy(buffer, startIndex, cmd.ResponseParameters, 0, paramLen);
                    startIndex += paramLen;
                }
            }
            cmd.IsFullyParsed = true;
            return cmd;
        }

    }
}
