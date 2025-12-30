using System;
using System.Windows;
using System.Windows.Input;
using Rongmeng_20251223.ViewModels;
using Rongmeng_20251223.LH; // 假设您的 ClientApi 在这里

namespace Rongmeng_20251223
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            var myApi = ClientApi.BuildClient(null);
            var vm = new LoginViewModel(myApi);
            vm.CloseAction = new Action(this.Close);

            vm.SetPasswordAction = new Action<string>((pwd) =>
            {
                this.TxtPassword.Password = pwd;
            });

            this.DataContext = vm;
        }
    }
}