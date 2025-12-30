using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Rongmeng_20251223.Interface.Model;
using Rongmeng_20251223.LH;
using Rongmeng_20251223.Service; // [新增] 引用 Service 命名空间

namespace Rongmeng_20251223.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // [新增] 引入业务服务，替代原来的 ClientApi 直接引用
        private readonly DeviceBusinessService _deviceService;

        // [新增] 引入配置服务，替代原来的文件读取逻辑
        private readonly StationConfigService _configService;

        public ObservableCollection<CameraControlItem> CameraControls { get; }

        // [保留] UI 状态属性
        private bool _isConnecting;
        private bool _isDisConnecting;
        private string _statusText = "系统就绪...";
        private TcpDeviceinfo _deviceInfo;

        // [保留] 连接状态 (用于控制按钮是否可用)
        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    RefreshAllCommands();
                }
            }
        }

        // [保留] 属性定义
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public TcpDeviceinfo DeviceInfo
        {
            get => _deviceInfo;
            set => SetProperty(ref _deviceInfo, value);
        }

        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                if (SetProperty(ref _isConnecting, value))
                {
                    OnPropertyChanged(nameof(ConnectButtonText));
                    ConnectCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsDisConnecting
        {
            get => _isDisConnecting;
            set
            {
                if (SetProperty(ref _isDisConnecting, value))
                {
                    OnPropertyChanged(nameof(DisConnectButtonText));
                    DisConnectCommand.NotifyCanExecuteChanged();
                }
            }
        }
        public string ConnectButtonText => IsConnecting ? "正在连接..." : "连接";
        public string DisConnectButtonText => IsDisConnecting ? "正在断开..." : "断开连接";

        // [保留] 自动化测试相关属性
        private bool _isAutoTesting;
        public bool IsAutoTesting { get => _isAutoTesting; set => SetProperty(ref _isAutoTesting, value); }

        private string _currentTestItemName;
        public string CurrentTestItemName { get => _currentTestItemName; set => SetProperty(ref _currentTestItemName, value); }

        private string _currentTestPrompt;
        public string CurrentTestPrompt { get => _currentTestPrompt; set => SetProperty(ref _currentTestPrompt, value); }

        private TaskCompletionSource<bool> _userInputSignal;

        public IAsyncRelayCommand StartAutoTestCommand { get; }
        public IRelayCommand<string> UserJudgmentCommand { get; }
        public IRelayCommand AuthorizeCommand { get; }
        public IRelayCommand RebootCommand { get; }
        public IRelayCommand TurnOnLedCommand { get; }
        public IRelayCommand TurnOffLedCommand { get; }
        public IRelayCommand TurnOnVideoCommand { get; }
        public IRelayCommand TurnOffVideoCommand { get; }
        public IAsyncRelayCommand ConnectCommand { get; }
        public IAsyncRelayCommand DisConnectCommand { get; }

        public MainViewModel(ClientApi api, string stationName)
        {
            _deviceService = new DeviceBusinessService(api);
            _configService = new StationConfigService();

            CameraControls = new ObservableCollection<CameraControlItem>();
            DeviceInfo = new TcpDeviceinfo();
            AuthorizeCommand = new RelayCommand(() => _deviceService.GetUid(), () => IsConnected);
            RebootCommand = new RelayCommand(() => _deviceService.Reboot(), () => IsConnected);
            TurnOnLedCommand = new RelayCommand(() => _deviceService.SetLed(true), () => IsConnected);
            TurnOffLedCommand = new RelayCommand(() => _deviceService.SetLed(false), () => IsConnected);
            TurnOnVideoCommand = new RelayCommand(() => _deviceService.ControlVideo(true), () => IsConnected);
            TurnOffVideoCommand = new RelayCommand(() => _deviceService.ControlVideo(false), () => IsConnected);

            StartAutoTestCommand = new AsyncRelayCommand(RunAutoTestSequence, () => IsConnected);
            UserJudgmentCommand = new RelayCommand<string>(OnUserJudgmentReceived);

            ConnectCommand = new AsyncRelayCommand(Connect, CanConnect);
            DisConnectCommand = new AsyncRelayCommand(DisConnect, CanDisConnect);

            LoadButtons(stationName);//分配按钮命令对象

            WeakReferenceMessenger.Default.Register<Messages>(this, (r, m) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AddLog(m.Value);

                    // 自动更新连接状态
                    if (m.Value.Contains("连接成功")) IsConnected = true;
                    else if (m.Value.Contains("已断开") || m.Value.Contains("连接失败")) IsConnected = false;
                });
            });
        }

        private void LoadButtons(string stationName)
        {
            try
            {
                var items = _configService.LoadStationConfig(stationName);

                CameraControls.Clear();
                foreach (var item in items)
                {
                    CameraControls.Add(new CameraControlItem
                    {
                        Content = item.Title,
                        ConfigData = item,
                        Command = new AsyncRelayCommand(
                            async () => await RunGenericTest(item),
                            () => IsConnected
                        )
                    });
                }
            }
            catch (Exception ex)
            {
                AddLog($"[配置加载失败] {ex.Message}");
            }
        }
        private async Task RunGenericTest(StationTestItem item)
        {
            if (!IsConnected)
            {
                MessageBox.Show("请先连接设备！");
                return;
            }
            try
            {
                // UI 逻辑：设置提示文字
                CurrentTestItemName = item.Title;
                CurrentTestPrompt = item.Tips;
                AddLog($"执行: {item.Title}");

                _deviceService.ExecuteTestItem(item);
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
            }
        }

        private async Task Connect()
        {
            try
            {
                IsConnecting = true;
                await Task.Run(() => _deviceService.Connect(DeviceInfo));
            }
            finally { IsConnecting = false; }
        }

        // [修改] 断开方法：调用 Service
        private async Task DisConnect()
        {
            try
            {
                IsDisConnecting = true;
                await Task.Run(() => _deviceService.Disconnect());
            }
            finally { IsDisConnecting = false; }
        }

        private bool CanConnect() => !IsConnecting;
        private bool CanDisConnect() => !IsDisConnecting;

        private void RefreshAllCommands()
        {
            if (CameraControls != null)
            {
                foreach (var item in CameraControls) item.Command.NotifyCanExecuteChanged();
            }
            StartAutoTestCommand.NotifyCanExecuteChanged();
            AuthorizeCommand.NotifyCanExecuteChanged();
            RebootCommand.NotifyCanExecuteChanged();
            TurnOnLedCommand.NotifyCanExecuteChanged();
            TurnOffLedCommand.NotifyCanExecuteChanged();
            TurnOnVideoCommand.NotifyCanExecuteChanged();
            TurnOffVideoCommand.NotifyCanExecuteChanged();
        }

        public void AddLog(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
            StatusText += logEntry;
        }

        private async Task RunAutoTestSequence()
        {
            if (IsAutoTesting) return;
            if (!IsConnected)
            {
                MessageBox.Show("请先连接设备！");
                return;
            }

            Dictionary<string, string> results = new Dictionary<string, string>();

            try
            {
                IsAutoTesting = true;
                AddLog(">>>>>> 开启自动化检测流程 <<<<<<");

                foreach (var step in CameraControls)
                {
                    StationTestItem config = step.ConfigData;
                    CurrentTestItemName = step.Content;
                    CurrentTestPrompt = string.IsNullOrEmpty(config?.Tips) ? $"正在执行 [{step.Content}]..." : config.Tips;

                    try
                    {
                        if (step.Command.CanExecute(null)) step.Command.Execute(null);
                    }
                    catch (Exception cmdEx) { AddLog($"[异常] {cmdEx.Message}"); }

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
                    _userInputSignal = new TaskCompletionSource<bool>();
                    bool isPass = await _userInputSignal.Task;

                    if (config != null && !string.IsNullOrEmpty(config.MesName))
                    {
                        results[config.MesName] = isPass ? "1" : "0";
                    }

                    if (!isPass)
                    {
                        AddLog($"[FAIL] {step.Content} -> 不合格");
                        MessageBox.Show($"测试在步骤 [{step.Content}] 失败！", "测试不合格", MessageBoxButton.OK, MessageBoxImage.Error);
                        GenerateAndLogResult(results);
                        return;
                    }
                    else
                    {
                        AddLog($"[PASS] {step.Content} -> 合格");
                    }
                    await Task.Delay(200);
                }

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
            // 这里仅仅是显示结果，如果需要上传 MES，应该调用 _deviceService.UploadMes(results)
            if (results.Count > 0)
            {
                string jsonResult = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.None);
                AddLog("--------------------------------");
                AddLog($"[最终结果JSON]: {jsonResult}");
                AddLog("--------------------------------");
            }
        }

        private void OnUserJudgmentReceived(string result)
        {
            _userInputSignal?.TrySetResult(result == "PASS");
        }
    }
}