using CommunityToolkit.Mvvm.Messaging;
using LHFactoryTool.LH;
using Rongmeng_20251223.Interface;
using Rongmeng_20251223.Interface.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Controls;

namespace Rongmeng_20251223.LH
{
    public interface IClienrApi
    {
        void Connect(TcpDeviceinfo info);
        void Send(IDocommand frame);
        void DisConnect();
    }
    public class ClientApi : IClienrApi
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Socket client;
        private bool isConnecting = false;
        private Thread threadReceive;
        private Thread threadReadBuffer;
        private readonly ManualResetEvent manualResetEvent = new ManualResetEvent(false);
        private byte[] buffers = new byte[0];
        private Queue<IDocommand> queuFrames = new Queue<IDocommand>();
        private IDocommand frame;
        ILHviedoApiCallBack lHviedoApiCallBack;
        private MemoryStream _msBuffer = new MemoryStream(1024 * 1024);
        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="frame"></param>
        public void Send(IDocommand frame)
        {
            try
            {
                client.Send(frame.ToBuffer());
            }
            catch (Exception ex)
            {
                logger.Debug(ex.ToString());
            }
        }
        /// <summary>
        /// 初始化
        /// </summary>
        /// <returns></returns>
        public static ClientApi BuildClient(ILHviedoApiCallBack VideoApiCallBack)
        {
            ClientApi lHVideoApi = new ClientApi
            {
                lHviedoApiCallBack = VideoApiCallBack
            };
            return lHVideoApi;
        }
        public void Receive()
        {
            String ip = "";
            try { ip = client.RemoteEndPoint.ToString(); } catch { }
            byte[] recvBuffer = new byte[1024 * 64];

            while (true)
            {
                try
                {
                    // --- A. 接收数据 ---
                    int r = client.Receive(recvBuffer);
                    if (r == 0) break; // 连接断开
                    _msBuffer.Seek(0, SeekOrigin.End);
                    _msBuffer.Write(recvBuffer, 0, r);

                    // --- C. 尝试解析 (循环处理，防止一次收到多包) ---
                    ParseLoop(ip);
                }
                catch (Exception ex)
                {
                    logger.ErrorFormat("连接异常断开：{0}，{1}", ip, ex.Message);
                    break;
                }
            }
        }
        public void Close() { }
        public void Dispose() { }
        public void Connect(TcpDeviceinfo info)
        {
            WeakReferenceMessenger.Default.Send(new Messages("开始连接..."));

            // 1. 清理旧连接逻辑保持不变
            if (client != null)
            {
                try
                {
                    if (client.Connected)
                    {
                        client.Shutdown(SocketShutdown.Both);
                    }
                    client.Close();
                }
                catch (Exception ex)
                {
                    logger.Debug("连接VIDEO:" + ex);
                }
                finally
                {
                    client = null;
                }
            }

            try
            {
                client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint EndPoint = new IPEndPoint(IPAddress.Parse(info.IPAddress), info.Port);
                manualResetEvent.Reset();

                client.BeginConnect(EndPoint, CallBackMethod, client);
                if (manualResetEvent.WaitOne(TimeSpan.FromSeconds(5), false))
                {
                    if (client != null && client.Connected)
                    {
                        StartThread();
                        WeakReferenceMessenger.Default.Send(new Messages("连接成功"));
                    }
                    else
                    {
                        throw new Exception("连接回调返回，但Socket未连接。");
                    }
                }
                else
                {
                    // 超时逻辑
                    if (client != null)
                    {
                        try { client.Close(); } catch { }
                        client = null;
                    }
                    WeakReferenceMessenger.Default.Send(new Messages("连接失败:超时"));
                    logger.Debug("连接失败");
                }
            }
            catch (Exception ex)
            {
                WeakReferenceMessenger.Default.Send(new Messages(ex.Message));
                logger.Debug(ex.ToString());
            }
        }

        private void StartThread()
        {
            // 开启接收线程
            threadReceive = new Thread(new ThreadStart(Receive)) { IsBackground = true };
            threadReceive.Start();
            threadReadBuffer = new Thread(new ThreadStart(StartReadBuffer)) { IsBackground = true };
            threadReadBuffer.Start();
        }

        // 在 ClientApi.cs 中找到这个方法并替换
        private void StartReadBuffer()
        {
            while (true)
            {
                try
                {
                    if (queuFrames.Count > 3)
                    {
                        lock (queuFrames)
                        {
                            queuFrames.Clear();
                            logger.Debug("渲染过慢，主动丢弃积压帧以释放内存");
                        }
                        continue;
                    }

                    if (queuFrames.Count <= 0)
                    {
                        Thread.Sleep(1); // 避免空转占用 CPU
                        continue;
                    }
                    frame = queuFrames.Dequeue();
                    if (frame != null)
                    {
                        using (Bitmap image = FFmpegDecoder.Instance.DecodeFrameToBitmap(frame.PlayLoad))
                        {
                            if (image != null)
                            {
                                lHviedoApiCallBack.GetBitmapImg(image);
                            }
                        }
                        frame = null;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("解码线程异常: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 将事件状态设置为有信号，从而开启tcp接收线程。
        /// </summary>
        /// <param name="asyncResult"></param>
        private void CallBackMethod(IAsyncResult asyncResult)
        {
            try
            {
                Socket thisSocket = (Socket)asyncResult.AsyncState;
                if (thisSocket != null)
                {
                    thisSocket.EndConnect(asyncResult);
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException sex)
            {
                // 这里捕获“目标机器拒绝连接”、“网络不可达”等真实错误
                logger.Debug("Socket连接失败: " + sex.Message);
            }
            catch (Exception ex)
            {
                logger.Debug("其他异常: " + ex.Message);
            }
            finally
            {
                manualResetEvent.Set();
            }
        }
        private void ParseLoop(string ip)
        {
            while (_msBuffer.Length > 0)
            {
                // 1. 至少要能读出头部长度信息 (前16个字节)
                if (_msBuffer.Length < 16) break;

                byte[] currentData = _msBuffer.ToArray();

                // 直接从 Header (第12-15字节) 读取 Body 长度
                int payloadLen = BitConverter.ToInt32(currentData, 12);

                // 2. 计算这个包的总大小 (Header 16字节 + Body长度)
                int packetSize = 16 + payloadLen;

                if (_msBuffer.Length >= packetSize)
                {

                    // 4. 解析 frame
                    IDocommand frame = SelectFactory.CreateDocomand().FromBuffer(currentData);
                    if (frame.IsFullyParsed)
                    {
                        uint type = BitConverter.ToUInt32(frame.PacketType, 0);

                        if (type == 0x00)
                        {
                            if (queuFrames.Count < 15)
                            {
                                queuFrames.Enqueue(frame);
                            }
                        }
                        else if (type == 0xF0) // === 命令响应 ===
                        {
                            WeakReferenceMessenger.Default.Send(new CommandResponseMessage(frame));
                            logger.Debug("收到设备命令响应");
                        }
                        else if (type == 0x01) // === IMU 数据 ===
                        {
                        }
                        else
                        {
                            logger.Warn($"收到未知类型包: {type}");
                        }
                    }
                    RemoveBytesFromStart(packetSize);
                }
                else
                {
                    break;
                }
            }
        }
        private void RemoveBytesFromStart(int count)
        {
            if (count <= 0) return;

            // 如果移除的长度超过了总长度，直接清空
            if (count >= _msBuffer.Length)
            {
                _msBuffer.SetLength(0);
                return;
            }

            byte[] buffer = _msBuffer.GetBuffer();
            long remaining = _msBuffer.Length - count;

            // 内存搬运：把后面的数据搬到最前面
            Buffer.BlockCopy(buffer, count, buffer, 0, (int)remaining);

            // 截断流
            _msBuffer.SetLength(remaining);
        }

        public void DisConnect()
        {
            WeakReferenceMessenger.Default.Send(new Messages("正在断开连接..."));
            logger.Debug("执行断开连接操作");
            queuFrames.Clear();
            if (client != null)
            {
                try
                {
                    // 只有连接状态下才需要 Shutdown，否则直接 Close
                    if (client.Connected)
                    {
                        try
                        {
                            client.Shutdown(SocketShutdown.Both);
                        }
                        catch (Exception sdEx)
                        {
                            // Shutdown 有时在连接已丢失时会报错，记录即可，不影响后续关闭
                            logger.Debug("Socket Shutdown异常: " + sdEx.Message);
                        }
                    }

                    // 关闭 Socket
                    client.Close();
                }
                catch (Exception ex)
                {
                    logger.Debug("断开连接异常: " + ex.Message);
                }
                finally
                {
                    client = null;
                }
            }
            WeakReferenceMessenger.Default.Send(new Messages("已断开连接"));
        }
    }
}