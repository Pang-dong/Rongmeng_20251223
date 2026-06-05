using System.Windows;
using System.Windows.Input;

namespace Rongmeng_20251223
{
    public partial class ScanCodeWindow : Window
    {
        public string ScannedCode { get; set; }

        public ScanCodeWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TBox_SN.Focus();
        }

        private void Button_Click_Cancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Button_Click_Confirm(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TBox_SN.Text))
            {
                MessageBox.Show("请输入或扫描 SN 码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ScannedCode = TBox_SN.Text.Trim();
            DialogResult = true;
        }

        private void TBox_SN_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Button_Click_Confirm(sender, e);
        }
    }
}
