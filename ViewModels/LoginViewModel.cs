using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rongmeng_20251223.Service; // 存放 ConfigManager
using Rongmeng_20251223.Interface.Model; // 存放 AppConfig (注意检查命名空间是否匹配)
using Rongmeng_20251223.LH;      // 存放 ClientApi
using Rongmeng_20251223.LH.lh;

namespace Rongmeng_20251223.ViewModels
{
    // 移除 partial，改为标准类，防止生成器干扰
    public partial class LoginViewModel : ObservableObject
    {
        private readonly ClientApi _api;

        private string _userName;
        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        private bool _isRememberPassword;
        public bool IsRememberPassword
        {
            get => _isRememberPassword;
            set => SetProperty(ref _isRememberPassword, value);
        }

        // 找到你原来的 IsMesMode 定义，完全替换成下面这一大段：

        private bool _isMesMode = true;

        public bool IsMesMode
        {
            get => _isMesMode;
            set
            {
                if (SetProperty(ref _isMesMode, value))
                {
                    OnPropertyChanged(nameof(IsTestMode));
                }
            }
        }
        public bool IsTestMode
        {
            get => !_isMesMode;
            set
            {
                if (SetProperty(ref _isMesMode, !value))
                {
                    OnPropertyChanged(nameof(IsMesMode));
                }
            }
        }

        private string _selectedStation;
        public string SelectedStation
        {
            get => _selectedStation;
            set => SetProperty(ref _selectedStation, value);
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }


        public ObservableCollection<string> StationList { get; } = new ObservableCollection<string>();

        // 用于 UI 回调的 Action
        public Action<string> SetPasswordAction { get; set; }
        public Action CloseAction { get; set; }

        public LoginViewModel(ClientApi api)
        {
            _api = api;

            // 初始化工站列表
            StationList.Add("授权工站");
            StationList.Add("功能测试工站");
            StationList.Add("老化工站");

            // 加载本地 JSON 配置
            LoadConfig();

        }

        private void LoadConfig()
        {
            var config = ConfigManager.Load();

            // 直接赋值给上面定义的大写属性
            UserName = config.UserName;
            IsRememberPassword = config.IsRememberMe;
            IsMesMode = config.IsMesMode;

            // 恢复工站选择
            if (!string.IsNullOrEmpty(config.LastStation) && StationList.Contains(config.LastStation))
                SelectedStation = config.LastStation;
            else
                SelectedStation = StationList.Count > 0 ? StationList[0] : "";

            // 延迟一点点时间回填密码，确保 UI 已加载
            if (IsRememberPassword && !string.IsNullOrEmpty(config.Password))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    SetPasswordAction?.Invoke(config.Password);
                }));
            }
        }

        [RelayCommand]
        private async Task Login(object parameter)
        {
            ErrorMessage = "";
            var passwordBox = parameter as PasswordBox;
            string password = passwordBox?.Password ?? "";

            if (IsMesMode)
            {
                // MES 模式验证逻辑
                if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(password))
                {
                    ErrorMessage = "账号或密码不能为空";
                    return;
                }

                // 这里调用您的验证接口
                // 使用 Task.Run 包装以避免警告
                bool isSuccess = await Task.Run(() => {
                    // 模拟接口调用：admin / 123456
                    return UserName == "admin" && password == "123456";
                });

                if (!isSuccess)
                {
                    ErrorMessage = "账号或密码错误";
                    return;
                }
            }

            // 验证通过，保存配置
            SaveConfig(password);

            // 打开主界面
            OpenMainWindow();
        }

        private void SaveConfig(string password)
        {
            var config = new AppConfig
            {
                UserName = UserName,
                Password = IsRememberPassword ? password : "",
                IsRememberMe = IsRememberPassword,
                IsMesMode = IsMesMode,
                LastStation = SelectedStation
            };
            ConfigManager.Save(config);
        }

        private void OpenMainWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow mainWin = new MainWindow();
                mainWin.Show();
                CloseAction?.Invoke();
            });
        }
    }
}