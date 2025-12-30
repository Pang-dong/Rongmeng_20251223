using CommunityToolkit.Mvvm.Messaging;
using Rongmeng_20251223.Interface.Model;
using Rongmeng_20251223.Interface;
using Rongmeng_20251223.LH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Rongmeng_20251223.LH.MessageType;

namespace Rongmeng_20251223.Service
{
    public class DeviceBusinessService
    {
        private readonly ClientApi _api;
        private readonly LicenseService _licenseService; // 引入授权服务

        public DeviceBusinessService(ClientApi api)
        {
            _api = api;
            _licenseService = new LicenseService();

            // 注册响应消息，在这里处理业务逻辑，而不是在 ViewModel 里
            WeakReferenceMessenger.Default.Register<CommandResponseMessage>(this, (r, m) =>
            {
                ProcessBusinessLogic(m.Value);
            });
        }

        // 基础连接封装
        public void Connect(TcpDeviceinfo info) => _api.Connect(info);
        public void Disconnect() => _api.DisConnect();

        public void Reboot() => SendCommand(CommandType.Reboot);
        public void SetLed(bool isOn) => SendCommand(isOn ? CommandType.EnableLed : CommandType.DisableLed, 0);
        public void ControlVideo(bool isStart) => SendCommand(isStart ? CommandType.StartVideo : CommandType.StopVideo);
        public void GetUid() => SendCommand(CommandType.GetUid);
        public void ExecuteTestItem(StationTestItem item)
        {
            ushort cmdId = Convert.ToUInt16(item.Command, 16);
            CommandType type = (CommandType)cmdId;
            string paramType = item.ParamType?.ToLower() ?? "none";

            IDocommand docommand;
            switch (paramType)
            {
                case "int":
                    int.TryParse(item.ParamValue, out int intVal);
                    docommand = SelectFactory.CreateDocomandInt(MessageTypes.Command, type, intVal);
                    break;
                case "string":
                    docommand = SelectFactory.CreateDocomandStringAnd(MessageTypes.Command, type, item.ParamValue);
                    break;
                default:
                    docommand = SelectFactory.CreateDocomandIntArray(MessageTypes.Command, type);
                    break;
            }
            _api.Send(docommand);
        }
        private void SendCommand(CommandType type, int? intVal = null)
        {
            IDocommand cmd;
            if (intVal.HasValue)
                cmd = SelectFactory.CreateDocomandInt(MessageTypes.Command, type, intVal.Value);
            else
                cmd = SelectFactory.CreateDocomandIntArray(MessageTypes.Command, type);

            _api.Send(cmd);
        }

        private void ProcessBusinessLogic(IDocommand response)
        {
            ushort cmdId = BitConverter.ToUInt16(response.CommandType, 0);
            CommandType type = (CommandType)cmdId;
            if (response.ResponseStatus != 0x00)
            {
                Log($"[失败] 命令 {type} 执行失败，错误码: {response.ResponseStatus:X2}");
                return;
            }
            switch (type)
            {
                case CommandType.GetUid:
                    ulong uidVal = BitConverter.ToUInt64(response.ResponseParameters, 0);
                    string uidStr = uidVal.ToString();
                    Log($"设备UID: {uidStr}");

                    // 自动授权逻辑
                    if (!string.IsNullOrEmpty(uidStr))
                    {
                        string code = _licenseService.GetLicenseCode(uidStr);
                        if (!string.IsNullOrEmpty(code))
                        {
                            Log($"获取授权成功，下发授权码...");
                            var cmd = SelectFactory.CreateDocomandStringAnd(MessageTypes.Command, CommandType.SetLicenceNo, code);
                            _api.Send(cmd);
                        }
                        else
                        {
                            Log("获取授权失败");
                        }
                    }
                    break;

                case CommandType.SetLicenceNo:
                    Log("设置授权码成功");
                    break;
                case CommandType.EnableLed: Log("LED已打开"); break;
                case CommandType.DisableLed: Log("LED已关闭"); break;
            }
        }
        private void Log(string msg) => WeakReferenceMessenger.Default.Send(new Messages(msg));
    }
}
