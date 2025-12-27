using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Rongmeng_20251223.Interface.Model;
using System.Windows;
using Rongmeng_20251223.LH;
using CommunityToolkit.Mvvm.Messaging;
using Rongmeng_20251223.Interface;
using static Rongmeng_20251223.LH.MessageType;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json.Linq;
using System.Windows.Markup;
using RestSharp;
using Rongmeng_20251223.Service;
using FFmpeg.AutoGen;

namespace Rongmeng_20251223.ViewModels
{
    public partial class MainViewModel:ObservableObject
    {
        public ObservableCollection<CameraControlItem> CameraControls { get; }
        private bool _isConnecting;
        private bool _isDisConnecting;
        private ClientApi lHviedoApi;
        private string _statusText = "系统就绪..."; // 默认文字
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }
        private TcpDeviceinfo _deviceInfo;

        /// <summary>
        /// 手动实现的 DeviceInfo 属性，不再依赖生成器
        /// </summary>
        public TcpDeviceinfo DeviceInfo
        {
            get => _deviceInfo;
            set
            {
                if (SetProperty(ref _deviceInfo, value))
                {
                    // 这里可以添加属性变更后的逻辑，比如通知命令刷新
                }
            }
        }
        // 手动实现 IsConnecting 属性
        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                if (SetProperty(ref _isConnecting, value))
                {
                    // 当状态改变时，手动通知关联属性更新
                    OnPropertyChanged(nameof(ConnectButtonText));
                    // 通知连接命令重新检查是否可用（变灰/变亮）
                    ConnectCommand.NotifyCanExecuteChanged();
                }
            }
        }
        public bool IsDisConnecting 
        {
            get =>_isDisConnecting;
            set
            {
                if(SetProperty(ref _isDisConnecting, value))
                {
                    OnPropertyChanged(nameof(DisConnectButtonText));
                    DisConnectCommand.NotifyCanExecuteChanged();
                }
            }
        }

        // 只读属性：按钮文字
        public string ConnectButtonText => IsConnecting ? "正在连接..." : "连接";
        public string DisConnectButtonText => IsDisConnecting ? "正在断开..." : "断开连接";
        // 用于控制判定框显示的开关
        private bool _isAutoTesting;
        public bool IsAutoTesting { get => _isAutoTesting; set => SetProperty(ref _isAutoTesting, value); }

        // 当前正在测的项目名称
        private string _currentTestItemName;
        public string CurrentTestItemName { get => _currentTestItemName; set => SetProperty(ref _currentTestItemName, value); }

        // 给用户的提示文字
        private string _currentTestPrompt;
        public string CurrentTestPrompt { get => _currentTestPrompt; set => SetProperty(ref _currentTestPrompt, value); }

        // 核心：用于暂停代码运行，直到用户点击按钮的“信号灯”
        private TaskCompletionSource<bool> _userInputSignal;

        // 自动化测试命令和判定命令
        public IAsyncRelayCommand StartAutoTestCommand { get; }
        public IRelayCommand<string> UserJudgmentCommand { get; }

        // 1. 手动定义命令属性
        public IRelayCommand AuthorizeCommand { get; }
        public IRelayCommand RebootCommand { get; }
        public IRelayCommand TurnOnLedCommand { get; }
        public IRelayCommand TurnOffLedCommand { get; }
        public IAsyncRelayCommand ConnectCommand { get; }
        public IRelayCommand TurnOnVideoCommand { get; }
        public IRelayCommand TurnOffVideoCommand { get; }
        public IAsyncRelayCommand DisConnectCommand { get; }

        public MainViewModel(ClientApi api)
        {
            // 2. 在构造函数里初始化命令
            AuthorizeCommand = new RelayCommand(Authorize);
            RebootCommand = new RelayCommand(Reboot);
            TurnOnLedCommand = new RelayCommand(TurnOnLed);
            TurnOffLedCommand = new RelayCommand(TurnOffLed);
            TurnOnVideoCommand = new RelayCommand(TurnOnVideo);
            TurnOffVideoCommand = new RelayCommand(TurnOffVideo);
            StartAutoTestCommand = new AsyncRelayCommand(RunAutoTestSequence);
            UserJudgmentCommand = new RelayCommand<string>(OnUserJudgmentReceived);
            this.lHviedoApi = api;
            WeakReferenceMessenger.Default.Register<Messages>(this, (r, m) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AddLog(m.Value);
                });
            });//日志显示在界面上
            WeakReferenceMessenger.Default.Register<CommandResponseMessage>(this, (r, m) =>
            {
                IDocommand responseFrame = m.Value;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProcessResponse(responseFrame);
                });
            });//消息分流后获取方法

            // 3. 初始化列表
            CameraControls = new ObservableCollection<CameraControlItem>
            {
                new CameraControlItem { Content = "摄像头授权", Command = AuthorizeCommand },
                //new CameraControlItem { Content = "重启摄像头", Command = RebootCommand },
                new CameraControlItem { Content = "打开LED",   Command = TurnOnLedCommand },
                new CameraControlItem { Content = "关闭LED",   Command = TurnOffLedCommand },
                new CameraControlItem { Content = "打开视频流", Command = TurnOnVideoCommand },
                new CameraControlItem { Content = "关闭视频流",  Command = TurnOffVideoCommand }
            };
            ConnectCommand = new AsyncRelayCommand(Connect, CanConnect);
            DisConnectCommand = new AsyncRelayCommand(DisConnect, CanDisConnect);
            DeviceInfo = new TcpDeviceinfo();
        }

        private async Task RunAutoTestSequence()
        {
            if (IsAutoTesting) return;

            if (lHviedoApi == null)
            {
                MessageBox.Show("请先连接设备！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                IsAutoTesting = true; // 【关键】显示判定框
                AddLog(">>>>>> 开启自动化检测流程 <<<<<<");

                await Task.Delay(300);

                foreach (var step in CameraControls)
                {
                    CurrentTestItemName = step.Content;
                    CurrentTestPrompt = $"正在执行 [{step.Content}]，请观察现象...";

                    // 4. 【关键修改】在循环内部捕获命令异常
                    // 这样即使“摄像头授权”失败了，程序也不会崩，而是让你点 FAIL
                    try
                    {
                        if (step.Command.CanExecute(null))
                        {
                            step.Command.Execute(null);
                        }
                    }
                    catch (Exception cmdEx)
                    {
                        AddLog($"[警告] 指令发送异常: {cmdEx.Message}");
                        CurrentTestPrompt = "指令发送失败！请检查连接，或直接判定为 FAIL。";
                    }
                    _userInputSignal = new TaskCompletionSource<bool>();
                    bool isPass = await _userInputSignal.Task;

                    // 6. 处理判定结果
                    if (isPass)
                    {
                        AddLog($"[PASS] {step.Content} -> 合格");
                    }
                    else
                    {
                        AddLog($"[FAIL] {step.Content} -> 不合格，测试终止。");
                        MessageBox.Show($"测试在步骤 [{step.Content}] 失败！", "测试不合格", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 步骤间稍微停顿，体验更好
                    await Task.Delay(200);
                }
            }
            catch (Exception ex)
            {
                AddLog($"流程异常中断: {ex.Message}");
                MessageBox.Show($"发生错误: {ex.Message}");
            }
            finally
            {
                // 只有流程彻底结束（跑完或报错）才隐藏框
                IsAutoTesting = false;
            }
        }

        private void OnUserJudgmentReceived(string result)
        {
            _userInputSignal?.TrySetResult(result == "PASS");
        }

        private void ProcessResponse(IDocommand responseFrame)
        {
            if (responseFrame.CommandType == null || responseFrame.CommandType.Length < 2)
            {
                AddLog("收到无效的命令响应（CommandType为空）");
                return;
            }
            ushort cmdId = BitConverter.ToUInt16(responseFrame.CommandType, 0);
            CommandType type = (CommandType)cmdId;
            if (responseFrame.ResponseStatus != 0x00)
            {
                AddLog($"[失败] 命令 {type} 执行失败，错误码: {responseFrame.ResponseStatus:X2}");
                return;
            }
            switch (type) 
            {
                case  CommandType.GetUid:
                    ulong UID = BitConverter.ToUInt64(responseFrame.ResponseParameters, 0);
                    AddLog($"设备UID: {UID}");
                    if (UID.ToString().Length!=0)
                    {
                        LicenseService service = new LicenseService();
                        string uid = UID.ToString();
                        string code = service.GetLicenseCode(uid);
                        if (!string.IsNullOrEmpty(code))
                        {
                            AddLog("获取授权成功，授权码：" + code);
                            IDocommand docommand = SelectFactory.CreateDocomandStringAnd(MessageTypes.Command, CommandType.SetLicenceNo,code);
                            lHviedoApi.Send(docommand);
                        }
                        else
                        {
                            AddLog("获取授权失败，请检查网络或日志");
                        }
                    }
                    break;
                    case CommandType.SetLicenceNo:
                    if (responseFrame.ResponseStatus == 0x00)
                    {
                        AddLog("设置授权码成功");
                    }
                    else
                    {
                        AddLog("设置授权码失败");
                    }
                    break;
                case CommandType.DisableLed:
                    if (responseFrame.ResponseStatus == 0x00)
                    {
                        AddLog("关闭LED成功");
                    }
                    else
                    {
                        AddLog("关闭LED失败");
                    }
                    break;
                    case CommandType.EnableLed:
                    if (responseFrame.ResponseStatus == 0x00)
                        {
                        AddLog("打开LED成功");
                        }
                        else
                        {
                            AddLog("打开LED失败");
                        }
                    break;
            }

        }

        private void Authorize()
        {
            IDocommand docommand  =SelectFactory.CreateDocomandIntArray(MessageTypes.Command, CommandType.GetUid);
            lHviedoApi.Send(docommand);
        }
        private void Reboot()
        {
            
            string datatime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            MessageBox.Show("正在执行：重启摄像头");
        }
        private void TurnOnLed()
        {
            IDocommand docommand = SelectFactory.CreateDocomandInt(MessageTypes.Command, CommandType.EnableLed,0);
            lHviedoApi.Send(docommand);
        }
        private void TurnOffLed()
        {
            IDocommand docommand = SelectFactory.CreateDocomandInt(MessageTypes.Command, CommandType.DisableLed,0);
            lHviedoApi.Send(docommand);
        }
        private void TurnOnVideo()
        {
            IDocommand docommand = SelectFactory.CreateDocomandIntArray(MessageTypes.Command, CommandType.StartVideo);
            lHviedoApi.Send(docommand);
        }
        private void TurnOffVideo()
        {
            IDocommand docommand = SelectFactory.CreateDocomandIntArray(MessageTypes.Command, CommandType.StopVideo);
            lHviedoApi.Send(docommand);
        }
        private bool CanConnect()
        {
            return !IsConnecting;
        }
        private bool CanDisConnect()
        {
            return !IsDisConnecting;
        }
        private async Task Connect()
        {
            try
            {
                IsConnecting = true;
                await Task.Run(async () =>
                {
                    lHviedoApi.Connect(DeviceInfo);
                } );
            }
            finally
            {
                IsConnecting = false;
            }
        }
        private async Task DisConnect()
        {
            try
            {
                IsDisConnecting = true;
                await Task.Run(async () =>
                {
                    lHviedoApi.DisConnect();
                });
            }
            finally
            {
                IsDisConnecting = false;
            }
        }
        public void AddLog(string message)
        {
            // 加上时间戳，并换行
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";

            // 追加到现有文本后面
            StatusText += logEntry;
        }
        private string ParseStringPayload(IDocommand frame)
        {
            if (frame.ResponseParameters == null || frame.ResponseParameters.Length == 0)
                return "空数据";
            return System.Text.Encoding.ASCII.GetString(frame.ResponseParameters).Trim('\0');
        }
    }
}
