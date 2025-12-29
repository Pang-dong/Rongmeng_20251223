using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel; // 只要引用这个即可
using CommunityToolkit.Mvvm.Input;
using Rongmeng_20251223.Service;
using Rongmeng_20251223.Interface.Model;
using Rongmeng_20251223.LH;
using Rongmeng_20251223.LH.lh;

namespace Rongmeng_20251223.ViewModels
{
    // 移除 partial，改为标准类，手动实现属性最稳定
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

        private bool _isMesMode = true;
        public bool IsMesMode
        {
            get => _isMesMode;
            set
            {
                if (SetProperty(ref _isMesMode, value))
                {
                    // 当 IsMesMode 变化时，通知界面刷新 IsTestMode
                    OnPropertyChanged(nameof(IsTestMode));
                }
            }
        }

        public bool IsTestMode
        {
            get => !_isMesMode;
            set
            {
                // 点击测试模式时，把 IsMesMode 设为反向值
                if (value == true)
                {
                    IsMesMode = false;
                }
                else
                {
                    IsMesMode = true;
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

        public Action<string> SetPasswordAction { get; set; }
        public Action CloseAction { get; set; }

        public LoginViewModel(ClientApi api)
        {
            _api = api;

            StationList.Add("授权工站");
            StationList.Add("功能测试");
            StationList.Add("老化工站");

            // 读取配置
            LoadConfig();
        }

        private void LoadConfig()
        {
            var config = ConfigManager.Load();
            UserName = config.UserName;
            IsRememberPassword = config.IsRememberMe;
            IsMesMode = config.IsMesMode;

            if (!string.IsNullOrEmpty(config.LastStation) && StationList.Contains(config.LastStation))
                SelectedStation = config.LastStation;
            else
                SelectedStation = StationList.Count > 0 ? StationList[0] : "";

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
                if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(password))
                {
                    ErrorMessage = "账号或密码不能为空";
                    return;
                }

                // 模拟验证
                bool isSuccess = await Task.Run(() => {
                    return UserName == "admin" && password == "123456";
                });

                if (!isSuccess)
                {
                    ErrorMessage = "账号或密码错误";
                    return;
                }
            }

            SaveConfig(password);
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
                MainWindow mainWin = new MainWindow(SelectedStation);
                mainWin.Show();
                CloseAction?.Invoke();
            });
        }
    }
}