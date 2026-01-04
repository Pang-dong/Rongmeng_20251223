using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json;
using Rongmeng_20251223.Interface.Model;
using Rongmeng_20251223.LH;
using Rongmeng_20251223.LH.lh;
using Rongmeng_20251223.Service;
using static System.Net.WebRequestMethods;
using static Rongmeng_20251223.LH.lh.ReturnResult;

namespace Rongmeng_20251223.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DeviceBusinessService _deviceService;
        private readonly StationConfigService _configService;
        private readonly WriteTestResultService _writeTestResultService;

        public ObservableCollection<CameraControlItem> CameraControls { get; }

        private bool _isConnecting;
        private bool _isDisConnecting;
        private string _statusText = "系统就绪...";
        private TcpDeviceinfo _deviceInfo;

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
        private string _wlanMac;//WLAN MAC地址 控件模板
        public string WlanMac
        {
            get => _wlanMac;
            set => SetProperty(ref _wlanMac, value);
        }
        // 控制判定按钮（PASS/FAIL）是否可见
        private bool _isJudgmentButtonsVisible;
        public bool IsJudgmentButtonsVisible
        {
            get => _isJudgmentButtonsVisible;
            set => SetProperty(ref _isJudgmentButtonsVisible, value);
        }
        private bool _isVideoPlaying;//是否在播放视频
        public bool IsVideoPlaying
        {
            get => _isVideoPlaying;
            set => SetProperty(ref _isVideoPlaying, value);
        }
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

        private bool _isAutoTesting;
        public bool IsAutoTesting 
        { 
            get => _isAutoTesting; 
            set
            {
                if (SetProperty(ref _isAutoTesting, value))
                {
                    RefreshAllCommands();
                }
            }
        }

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
            _writeTestResultService = new WriteTestResultService();

            CameraControls = new ObservableCollection<CameraControlItem>();
            DeviceInfo = new TcpDeviceinfo();
            AuthorizeCommand = new RelayCommand(() => _deviceService.GetUid(), () => IsConnected);
            RebootCommand = new RelayCommand(() => _deviceService.Reboot(), () => IsConnected);
            TurnOnLedCommand = new RelayCommand(() => _deviceService.SetLed(true), () => IsConnected);
            TurnOffLedCommand = new RelayCommand(() => _deviceService.SetLed(false), () => IsConnected);
            TurnOnVideoCommand = new RelayCommand(() => _deviceService.ControlVideo(true), () => IsConnected);
            TurnOffVideoCommand = new RelayCommand(() => _deviceService.ControlVideo(false), () => IsConnected);

            // [修改] 3. 在打开视频命令中设置 IsVideoPlaying = true
            TurnOnVideoCommand = new RelayCommand(() =>
            {
                _deviceService.ControlVideo(true);
                IsVideoPlaying = true; // 切换界面显示视频
            }, () => IsConnected);

            // [修改] 4. 在关闭视频命令中设置 IsVideoPlaying = false
            TurnOffVideoCommand = new RelayCommand(() =>
            {
                _deviceService.ControlVideo(false);
                IsVideoPlaying = false; // 切换界面显示信息面板
            }, () => IsConnected);

            StartAutoTestCommand = new AsyncRelayCommand(RunAutoTestSequence, () => IsConnected);
            UserJudgmentCommand = new RelayCommand<string>(OnUserJudgmentReceived);

            ConnectCommand = new AsyncRelayCommand(Connect, CanConnect);
            DisConnectCommand = new AsyncRelayCommand(DisConnect, CanDisConnect);

            LoadButtons(stationName);//加载按钮

            WeakReferenceMessenger.Default.Register<Messages>(this, (r, m) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AddLog(m.Value);
                    if (m.Value.Contains("连接成功")) IsConnected = true;
                    else if (m.Value.Contains("已断开") || m.Value.Contains("连接失败")) IsConnected = false;
                });
            });
        }

        // 位置：MainViewModel.cs -> LoadButtons 方法

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
                            () => IsConnected && !IsAutoTesting
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

        private async Task DisConnect()
        {
            try
            {
                IsDisConnecting = true;
                await Task.Run(() => _deviceService.Disconnect());
            }
            finally { IsDisConnecting = false; }
        }

        // [修改] 连接条件：正在连接时不可点，且已经连接成功后也不可点
        private bool CanConnect() => !IsConnecting && !IsConnected;
        private bool CanDisConnect() => !IsDisConnecting;

        private void RefreshAllCommands()
        {
            if (CameraControls != null)
            {
                foreach (var item in CameraControls) item.Command.NotifyCanExecuteChanged();
            }
            StartAutoTestCommand.NotifyCanExecuteChanged();
            ConnectCommand.NotifyCanExecuteChanged();
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
                IsAutoTesting = true; // 1. 设置标志位，界面触发器会缩小日志高度，禁用手动按钮
                AddLog(">>>>>> 开启自动化检测流程 <<<<<<");

                // 2. 重置所有按钮状态（颜色恢复默认）
                foreach (var c in CameraControls) c.TestState = 0;

                foreach (var step in CameraControls)
                {
                    StationTestItem config = step.ConfigData;
                    CurrentTestItemName = step.Content;
                    CurrentTestPrompt = string.IsNullOrEmpty(config?.Tips) ? $"正在执行 [{step.Content}]..." : config.Tips;

                    // 隐藏判定按钮，防止误触
                    IsJudgmentButtonsVisible = false;

                    try
                    {
                        // 执行测试指令
                        _deviceService.ExecuteTestItem(config);
                    }
                    catch (Exception cmdEx) { AddLog($"[异常] {cmdEx.Message}"); }

                    // 倒计时逻辑
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
                    else
                    {
                        CurrentTestPrompt += " (请判定)";
                    }

                    // 显示判定按钮，等待用户点击
                    IsJudgmentButtonsVisible = true;

                    _userInputSignal = new TaskCompletionSource<bool>();
                    bool isPass = await _userInputSignal.Task;

                    // 记录MES数据
                    if (config != null && !string.IsNullOrEmpty(config.MesName))
                    {
                        results[config.MesName] = isPass ? "1" : "0";
                    }

                    if (!isPass)
                    {
                        step.TestState = 2; // [关键] 失败变红
                        AddLog($"[FAIL] {step.Content} -> 不合格");
                        MessageBox.Show($"测试在步骤 [{step.Content}] 失败！", "测试不合格", MessageBoxButton.OK, MessageBoxImage.Error);
                        GenerateAndLogResult(results);
                        return; // 失败直接中断
                    }
                    else
                    {
                        step.TestState = 1; // [关键] 成功变绿
                        AddLog($"[PASS] {step.Content} -> 合格");
                    }
                    // 稍微停顿，让用户看清变绿的效果
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
                IsAutoTesting = false; // 恢复状态
                IsJudgmentButtonsVisible = false;
                CurrentTestPrompt = "测试结束";
                RefreshAllCommands(); // 刷新界面按钮的可用状态
            }
        }

        private void GenerateAndLogResult(Dictionary<string, string> results)
        {
            if (results.Count > 0)
            {
                string jsonResult = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.None);
                var config = ConfigManager.Load();//必须要这样把config的配置读出来
                if (config.IsMesMode)
                {
                    string url = $"http://{config.FtpIp}:8017/Service.asmx";
                    string finalUploadJson = _writeTestResultService.EnrichJsonData(jsonResult);
                    var args = new Dictionary<string, object>
                    {
                        { "_WriteTestResult", finalUploadJson }
                    };
                    Task.Run(async () =>
                    {
                        try
                        {
                            // 3. 调用通用方法，传入字典
                            string response = await InvokeMESInterface.PostToMesAsync(url, "WriteTestResultInfo", args);
                            if (string.IsNullOrEmpty(response) || response.Contains("ERROR"))
                            {
                                AddLog($"接口调用失败: {response}");
                                return;
                            }
                            var result = JsonConvert.DeserializeObject<BaseResult>(response);
                            if (result != null && result.IsSuccess)
                            {
                                AddLog("MES数据更新成功");
                            }
                            else
                            {
                                string failMsg = result?.msg ?? "未知错误";
                                AddLog(failMsg );
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"MES上传异常: {ex.Message}");
                        }
                    });
                }
            }
        }

        private void OnUserJudgmentReceived(string result)
        {
            _userInputSignal?.TrySetResult(result == "PASS");
        }
    }
}