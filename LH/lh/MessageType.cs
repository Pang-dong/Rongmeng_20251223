using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.LH
{
    public  class MessageType
    {
        public enum MessageTypes
        {
            /// <summary>图像数据消息</summary>
            ImageData = 0x00,

            /// <summary>IMU数据消息</summary>
            ImuData = 0x01,

            /// <summary>命令消息</summary>
            Command = 0x02,

            /// <summary>响应消息</summary>
            Response = 0xF0,

            /// <summary>未知类型</summary>
            Unknown = 0xFF
        }
        /// <summary>
        /// 命令类型枚举 - 定义各种系统命令 (与C代码中的MSG_CMD_TYPE_E_枚举匹配)
        /// </summary>
        public enum CommandType
        {
            /// <summary>获取版本信息</summary>
            GetVersion = 0x0000,

            /// <summary>获取设备UID</summary>
            GetUid = 0x0001,

            /// <summary>获取许可证编号</summary>
            GetLicenceNo = 0x0002,

            /// <summary>设置许可证编号</summary>
            SetLicenceNo = 0x0003,

            /// <summary>获取客户端密钥</summary>
            GetClientKey = 0x0004,

            /// <summary>设置客户端密钥</summary>
            SetClientKey = 0x0005,

            /// <summary>设置系统时间</summary>
            SetSystemTime = 0x0011,

            /// <summary>获取系统时间</summary>
            GetSystemTime = 0x0012,

            /// <summary>启用LED</summary>
            EnableLed = 0x0013,

            /// <summary>禁用LED</summary>
            DisableLed = 0x0014,

            /// <summary>恢复出厂设置</summary>
            RestoreFactory = 0xF000,

            /// <summary>重启设备</summary>
            Reboot = 0xF001,

            /// <summary>开始视频流</summary>
            StartVideo = 0x000E,

            /// <summary>停止视频流</summary>
            StopVideo = 0x000F,

            /// <summary>心跳包</summary>
            Heartbeat = 0x0030,

            /// <summary>获取设备状态</summary>
            GetStatus = 0x0031,

            /// <summary>未知命令</summary>
            Unknown = 0xFFFF
        }
    }
}
