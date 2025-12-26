using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.Interface.Model
{
    public  class Docomand
    {
        public byte[] MagicNum { get; set; } = { 0x55, 0x49 };
        public byte Version { get; set; } = 0;
        public byte DeviceID { get; set; } = 0;
        public byte PacketCount { get; set; } = 1;
        public byte PackID { get; set; } = 1;
        public byte[] Reserved { get; set; } = new byte[4];
        public byte Staus { get; set; }
        public byte DataBcc { get; set; }
        public byte[] PlayloadLenth { get; set; } = new byte[4];
        #region 消息包字段 (Layer 2)
        public byte[] PacketType { get; set; } = new byte[4]; // MsgType
        #endregion
        #region 业务负载字段 (Layer 3)
        public byte[] CommandType { get; set; } = new byte[4]; // CmdType
        public byte[] PlayLoad { get; set; }     // 参数或视频裸流
        #endregion
        #region 响应字段
        public bool IsResponse { get; set; }
        public byte ResponseStatus { get; set; }
        public byte[] ResponseParameters { get; set; } =Array.Empty<byte>();
        #endregion
        #region 解析状态
        public bool IsFullyParsed { get; set; }
        #endregion

    }
}
