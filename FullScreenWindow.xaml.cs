using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Rongmeng_20251223
{
    public partial class FullScreenWindow : Window
    {
        public FullScreenWindow()
        {
            InitializeComponent();
        }

        // 用于外部更新图片源
        public void UpdateImageSource(ImageSource source)
        {
            FullImage.Source = source;
        }

        // 1. 按 ESC 关闭
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        // 2. 双击画面关闭
        private void FullImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                this.Close();
            }
        }

        // 3. 点击按钮关闭
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}