using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.Interface
{
    public  interface IDocommand
    {
        #region 融梦协议
        byte[] MagicNum { get; set; }
        byte Version { get; set; }
        byte DeviceID { get; set; }
        byte PacketCount { get; set; }
        byte PackID { get; set; }   
        byte[] Reserved { get; set; }
        byte Staus { get; set; }    
        byte DataBcc { get; set; }
        byte[] PlayloadLenth { get; set; }
        #region 消息包字段
        byte[] PacketType { get; set; }
        #endregion
        #region 业务负载
        byte[] CommandType { get; set; }
        byte[] PlayLoad { get; set; }
        #endregion
        #region 4. 响应专用字段 (Response)
        bool IsResponse { get; set; }
        byte ResponseStatus { get; set; }
        byte[] ResponseParameters { get; set; }
        #endregion

        #region 5. 解析状态与方法
        /// <summary>
        /// 标记是否成功解析完整包
        /// </summary>
        bool IsFullyParsed { get; set; }

        /// <summary>
        /// 从字节缓冲区解析数据并填充属性
        /// </summary>
        /// <param name="buffer">接收到的字节数组</param>
        /// <returns>解析后的接口实例</returns>
        IDocommand FromBuffer(byte[] buffer);

        /// <summary>
        /// 将当前对象序列化为字节数组 (用于发送)
        /// </summary>
        /// <returns>完整的协议字节流</returns>
        byte[] ToBuffer();
        #endregion

        #endregion
    }
}
