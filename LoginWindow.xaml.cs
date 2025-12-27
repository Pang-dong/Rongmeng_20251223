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

            // 1. 初始化 ViewModel
            // 注意：这里需要传入您的 ClientApi 实例，根据您之前的代码逻辑
            var myApi = ClientApi.BuildClient(null);
            var vm = new LoginViewModel(myApi);

            // 2. 绑定关闭窗口的动作
            vm.CloseAction = new Action(this.Close);

            // 3. 绑定密码回填动作 (用于记住密码功能)
            vm.SetPasswordAction = new Action<string>((pwd) =>
            {
                this.TxtPassword.Password = pwd;
            });

            this.DataContext = vm;
        }

        // 如果您在 XAML 中添加了自定义标题栏，需要这个方法来实现拖动
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}