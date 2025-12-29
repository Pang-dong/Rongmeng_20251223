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
                    // 如果是第一次运行，或者视频分辨率变了（比如从720p变1080p），就需要重新申请一块内存
                    if (writeable == null ||
                        writeable.PixelWidth != bmp.Width ||
                        writeable.PixelHeight != bmp.Height)
                    {
                        writeable = new WriteableBitmap(
                            bmp.Width,
                            bmp.Height,
                            96, 96, // DPI 设置，一般 96 即可
                            System.Windows.Media.PixelFormats.Bgr24,
                            null);
                        PreviewImage.Source = writeable;
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

    }
}
