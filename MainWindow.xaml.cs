using LHFactoryTool.LH;
using log4net.Repository.Hierarchy;
using Rongmeng_20251223.Interface;
using Rongmeng_20251223.LH;
using Rongmeng_20251223.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing.Imaging;
using System.IO;
using System.Web;
using CommunityToolkit.Mvvm.Messaging;
using Rongmeng_20251223.Interface.Model;


namespace Rongmeng_20251223
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window,ILHviedoApiCallBack
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private ClientApi ClientApi;
        private WriteableBitmap writeable;
        private FullScreenWindow _fullScreenWindow;
        public MainWindow(string stationName = "")
        {
            InitializeComponent();
            ClientApi myApi = ClientApi.BuildClient(this);

            // 传给 ViewModel
            this.DataContext = new MainViewModel(myApi, stationName);

            FFmpegDecoder.Instance.Initialize(1920, 1080, VideoFormat.H264);
        }

        private void SavePictureButton_Click(object sender, RoutedEventArgs e) { }
        /// <summary>
        /// 出图
        /// </summary>
        /// <param name="bmp"></param>
        public void GetBitmapImg(System.Drawing.Bitmap bmp)
        {
            if (bmp == null) return;

            // 必须切到 UI 线程，因为 WriteableBitmap 属于 UI 对象
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 1. 初始化或重建“白板”
                    if (writeable == null ||
                        writeable.PixelWidth != bmp.Width ||
                        writeable.PixelHeight != bmp.Height)
                    {
                        writeable = new WriteableBitmap(
                            bmp.Width,
                            bmp.Height,
                            96, 96,
                            System.Windows.Media.PixelFormats.Bgr24,
                            null);
                        PreviewImage.Source = writeable;

                        // [新增] 如果全屏窗口开着，且 WriteableBitmap 重建了（例如分辨率变了），必须重新赋值给全屏窗口
                        if (_fullScreenWindow != null)
                        {
                            _fullScreenWindow.UpdateImageSource(writeable);
                        }
                    }

                    // 2. 锁定源数据 (System.Drawing.Bitmap)
                    // 这一步是为了拿到 raw 数据的内存指针 (Scan0)
                    BitmapData bmpData = bmp.LockBits(
                        new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                        ImageLockMode.ReadOnly,
                        bmp.PixelFormat);
                    try
                    {
                        writeable.WritePixels(
                            new System.Windows.Int32Rect(0, 0, bmp.Width, bmp.Height), // 更新区域：整个画面
                            bmpData.Scan0,                  // 源数据指针
                            bmpData.Stride * bmp.Height,    // 数据总大小
                            bmpData.Stride);                // 步长 (Stride)
                    }
                    finally
                    {
                        bmp.UnlockBits(bmpData);
                    }
                }
                catch (Exception ex)
                {
                    WeakReferenceMessenger.Default.Send(new Messages("渲染失败: " + ex.Message));
                    logger.Error("渲染失败: " + ex.Message);
                }
            });
        }

        private void ConnectionStatusText_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            LogScrollViewer.ScrollToBottom();
        }
        private void PreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) // 只有双击才触发
            {
                if (writeable == null) return; // 如果没有图像，不打开

                // 防止重复打开
                if (_fullScreenWindow == null)
                {
                    _fullScreenWindow = new FullScreenWindow();
                    // 订阅关闭事件，窗口关闭后置空引用
                    _fullScreenWindow.Closed += (s, args) => _fullScreenWindow = null;

                    // 传入当前的图像源
                    _fullScreenWindow.UpdateImageSource(writeable);

                    _fullScreenWindow.Show();
                }
                else
                {
                    _fullScreenWindow.Activate(); // 如果已打开，则激活到前台
                }
            }
        }
    }
}
