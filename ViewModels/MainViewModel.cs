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
using Newtonsoft.Json;
using System.IO;

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

        public MainViewModel(ClientApi api,string stationName)
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
            LoadConfigAndInitButtons(stationName);
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

        /// <summary>
        /// 从 JSON 文件加载配置并生成按钮
        /// </summary>
        private void LoadConfigAndInitButtons(string currentStation)
        {
            CameraControls.Clear();

            // 1. 拼接路径: 执行目录/Chinese/StationConfig.json
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chinese", "StationConfig.json");

            if (!File.Exists(configPath))
            {
                AddLog($"[错误] 未找到配置文件: {configPath}");
                return;
            }

            try
            {
                // 2. 读取并反序列化
                string json = File.ReadAllText(configPath);
                var allItems = JsonConvert.DeserializeObject<List<StationTestItem>>(json);

                if (allItems == null) return;

                // 3. 遍历生成按钮 (使用 Lambda 闭包捕获 item)
                foreach (var item in allItems)
                {
                    if (item.Station == currentStation)
                    {
                        var btn = new CameraControlItem
                        {
                            Content = item.Title,
                            ConfigData = item,
                            // 【简洁代码】直接使用 Lambda 表达式绑定执行逻辑
                            // 注意：这里用了 AsyncRelayCommand 因为 RunGenericTest 是异步的(有延时)
                            Command = new AsyncRelayCommand(async () => await RunGenericTest(item))
                        };
                        CameraControls.Add(btn);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"加载配置失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 通用的测试执行逻辑：发指令 + 提示 + 防呆延时
        /// </summary>
        private async Task RunGenericTest(StationTestItem item)
        {
            if (lHviedoApi == null)
            {
                MessageBox.Show("请先连接设备！");
                return;
            }

            try
            {
                // 1. 设置界面提示
                CurrentTestItemName = item.Title;
                CurrentTestPrompt = item.Tips;
                AddLog($"执行: {item.Title}");

                // 2. 解析并发送指令 (Hex String -> UInt16)
                ushort cmdId = Convert.ToUInt16(item.Command, 16);
                CommandType type = (CommandType)cmdId;

                // 简单指令直接发送，特殊指令(如鉴权)可在此处加 if 判断
                IDocommand docommand = SelectFactory.CreateDocomandIntArray(MessageTypes.Command, type);
                lHviedoApi.Send(docommand);

                // 3. 防呆倒计时 (Timeout)
                if (item.Timeout > 0)
                {
                    string originalTips = item.Tips;
                    for (int i = item.Timeout; i > 0; i--)
                    {
                        CurrentTestPrompt = $"{originalTips} (请等待 {i} 秒...)";
                        await Task.Delay(1000);
                    }
                    CurrentTestPrompt = $"{originalTips} (执行完成)";
                }
            }
            catch (Exception ex)
            {
                AddLog($"执行异常: {ex.Message}");
                CurrentTestPrompt = "指令发送失败";
            }
        }

        private async Task RunAutoTestSequence()
        {
            if (IsAutoTesting) return;
            if (lHviedoApi == null)
            {
                MessageBox.Show("请先连接设备！");
                return;
            }

            // 1. 定义结果字典
            Dictionary<string, string> results = new Dictionary<string, string>();

            try
            {
                IsAutoTesting = true;
                AddLog(">>>>>> 开启自动化检测流程 <<<<<<");

                foreach (var step in CameraControls)
                {
                    StationTestItem config = step.ConfigData;

                    // 2. 设置提示信息
                    CurrentTestItemName = step.Content;
                    CurrentTestPrompt = string.IsNullOrEmpty(config?.Tips)
                                        ? $"正在执行 [{step.Content}]..."
                                        : config.Tips;

                    // 3. 执行指令
                    try
                    {
                        if (step.Command.CanExecute(null))
                        {
                            step.Command.Execute(null);
                        }
                    }
                    catch (Exception cmdEx)
                    {
                        AddLog($"[异常] {cmdEx.Message}");
                    }

                    // 4. 防呆倒计时
                    int waitTime = config != null ? config.Timeout : 0;
                    if (waitTime > 0)
                    {
                        string basePrompt = CurrentTestPrompt;
                        for (int i = waitTime; i > 0; i--)
                        {
                            CurrentTestPrompt = $"{basePrompt} (锁定 {i}秒)";
                            await Task.Delay(1000);
                        }
                        CurrentTestPrompt = basePrompt + " (请判定)";
                    }

                    // 5. 等待用户判定 (Pass/Fail)
                    _userInputSignal = new TaskCompletionSource<bool>();
                    bool isPass = await _userInputSignal.Task;

                    // 6. 记录结果 (Pass=1, Fail=0)
                    if (config != null && !string.IsNullOrEmpty(config.MesName))
                    {
                        results[config.MesName] = isPass ? "1" : "0";
                    }

                    if (!isPass)
                    {
                        AddLog($"[FAIL] {step.Content} -> 不合格");
                        MessageBox.Show($"测试在步骤 [{step.Content}] 失败！", "测试不合格", MessageBoxButton.OK, MessageBoxImage.Error);
                        // 失败后立即生成结果并退出
                        GenerateAndLogResult(results);
                        return;
                    }
                    else
                    {
                        AddLog($"[PASS] {step.Content} -> 合格");
                    }

                    // 步骤间缓冲
                    await Task.Delay(200);
                }

                // 全部通过
                AddLog("所有测试项通过！");
                GenerateAndLogResult(results);
            }
            catch (Exception ex)
            {
                AddLog($"流程异常中断: {ex.Message}");
            }
            finally
            {
                IsAutoTesting = false;
                CurrentTestPrompt = "测试结束";
            }
        }
        private void GenerateAndLogResult(Dictionary<string, string> results)
        {
            if (results.Count > 0)
            {
                string jsonResult = JsonConvert.SerializeObject(results, Formatting.None);
                AddLog("--------------------------------");
                AddLog($"[最终结果JSON]: {jsonResult}");
                AddLog("--------------------------------");
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
