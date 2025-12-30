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
using System.Collections.Generic;
using Newtonsoft.Json;
using static Rongmeng_20251223.LH.lh.ReturnResult;

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
        private string _windowTitle;
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
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
        private string _ftpIp;
        public string FtpIp
        {
            get => _ftpIp;
            set => SetProperty(ref _ftpIp, value);
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
            var titleInfo = new AppTitleInfo();
            WindowTitle = titleInfo.FullTitle;
            StationList.Add("授权工站");
            StationList.Add("功能测试");
            StationList.Add("老化工站");
            LoadConfig();
        }

        private void LoadConfig()
        {
            var config = ConfigManager.Load();
            UserName = config.UserName;
            IsRememberPassword = config.IsRememberMe;
            IsMesMode = config.IsMesMode;
            FtpIp = string.IsNullOrEmpty(config.FtpIp) ? "192.168.88.144" : config.FtpIp;

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
                try
                {
                    var args = new Dictionary<string, object>
                    {
                        { "_username", UserName },
                        { "_password", password }
                    };
                    string url = $"http://{FtpIp}:8017/Service.asmx";
                    string jsonStr = await InvokeMESInterface.PostToMesAsync(url, "GetUserLoginInfo", args);

                    if (string.IsNullOrEmpty(jsonStr) || jsonStr.Contains("ERROR"))
                    {
                        ErrorMessage = $"接口调用失败: {jsonStr}";
                        return;
                    }
                    var result = JsonConvert.DeserializeObject<BaseResult>(jsonStr);
                    if (result != null && result.IsSuccess)
                    {
                        SaveConfig(password);
                        OpenMainWindow();
                    }
                    else
                    {
                        // 登录失败
                        string failMsg = result?.msg ?? "未知错误";
                        ErrorMessage = failMsg;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"系统异常: {ex.Message}";
                    return;
                }
            }
        }

        private void SaveConfig(string password)
        {
            var config = new AppConfig
            {
                UserName = UserName,
                Password = IsRememberPassword ? password : "",
                IsRememberMe = IsRememberPassword,
                IsMesMode = IsMesMode,
                LastStation = SelectedStation,
                FtpIp = FtpIp
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