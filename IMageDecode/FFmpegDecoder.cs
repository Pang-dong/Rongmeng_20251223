using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace LHFactoryTool.LH
{
    public enum VideoFormat
    {
        H264,
        H265,
        RawVideo
    }

    public unsafe class FFmpegDecoder : IDisposable
    {
        private AVCodecContext* _codecContext;
        private AVFrame* _frame; // 解码后的原始帧(通常是YUV)
        private AVPacket* _packet;
        private SwsContext* _swsContext;
        
        private readonly object _lockObject = new object();
        private bool _isInitialized = false;
        private int _width;
        private int _height;
        private VideoFormat _format;
        private bool _disposed = false;
        private static readonly Lazy<FFmpegDecoder> _lazyInstance =
            new Lazy<FFmpegDecoder>(() => new FFmpegDecoder());

        // 公开的静态属性，用于获取全局唯一实例
        public static FFmpegDecoder Instance => _lazyInstance.Value;

        // 缓存 Bitmap 对象（可选，如果上层逻辑允许复用）
        // 这里为了兼容性仍每次返回新 Bitmap，但写入过程已极大优化

        static FFmpegDecoder()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch = Environment.Is64BitProcess ? "x64" : "x86";
            string libraryPath = Path.Combine(baseDir, arch);

            if (Directory.Exists(libraryPath))
            {
                ffmpeg.RootPath = libraryPath;
            }
            ffmpeg.avformat_network_init();
        }

        public bool Initialize(int width, int height, VideoFormat format)
        {
            lock (_lockObject)
            {
                Cleanup();

                try
                {
                    _width = width;
                    _height = height;
                    _format = format;

                    //AVCodecID codecId = _format == VideoFormat.H264 ?
                    //    AVCodecID.AV_CODEC_ID_H264 : AVCodecID.AV_CODEC_ID_RAWVIDEO;
                    AVCodecID codecId;
                    switch (_format)
                    {
                        case VideoFormat.H264:
                            codecId = AVCodecID.AV_CODEC_ID_H264; // 对应 H.264
                            break;
                        case VideoFormat.H265:
                            codecId = AVCodecID.AV_CODEC_ID_HEVC;
                            break;
                        default:
                            codecId = AVCodecID.AV_CODEC_ID_RAWVIDEO;
                            break;
                    }

                    AVCodec* codec = ffmpeg.avcodec_find_decoder(codecId);
                    if (codec == null) return false;

                    _codecContext = ffmpeg.avcodec_alloc_context3(codec);
                    
                    // --- 优化配置 ---
                    _codecContext->codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO;
                    _codecContext->width = width;
                    _codecContext->height = height;

                    if (_format == VideoFormat.H264 || _format ==VideoFormat.H265)
                    {
                        // 【关键优化1】设置低延迟标志，告诉解码器不要缓冲帧
                        _codecContext->flags |= ffmpeg.AV_CODEC_FLAG_LOW_DELAY;
                        _codecContext->flags2 |= ffmpeg.AV_CODEC_FLAG2_FAST;
                        
                        // 【关键优化2】线程数设为1或极低值。多线程解码会引入帧缓冲延迟。
                        // 对于实时流，thread_count = 1 通常是最快的响应速度
                        _codecContext->thread_count = 1; 
                    }
                    else
                    {
                        _codecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
                    }

                    if (ffmpeg.avcodec_open2(_codecContext, codec, null) < 0) return false;

                    _frame = ffmpeg.av_frame_alloc();
                    _packet = ffmpeg.av_packet_alloc();

                    _isInitialized = true;
                    return true;
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Init Error: {ex.Message}");
                    Cleanup();
                    return false;
                }
            }
        }

        public Bitmap DecodeFrameToBitmap(byte[] frameData)
        {
            if (!_isInitialized || frameData == null || frameData.Length == 0) return null;

            lock (_lockObject)
            {
                try
                {
                    // 重置 Packet
                    ffmpeg.av_packet_unref(_packet);

                    // --- 优化数据拷贝 ---
                    // 检查是否需要添加 Start Code (00 00 00 01)
                    // H264通常需要 Start Code，有些流传输过来不带
                    bool hasStartCode = frameData.Length >= 4 &&
                                       ((frameData[0] == 0 && frameData[1] == 0 && frameData[2] == 0 && frameData[3] == 1) ||
                                        (frameData[0] == 0 && frameData[1] == 0 && frameData[2] == 1));

                    int allocSize = frameData.Length + (hasStartCode ? 0 : 4);
                    
                    // 使用 av_malloc 分配非托管内存
                    // 注意：FFmpeg 要求 buffer 有 padding
                    byte* buffer = (byte*)ffmpeg.av_malloc((ulong)allocSize + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE);

                    fixed (byte* srcPtr = frameData)
                    {
                        //if (!hasStartCode && _format == VideoFormat.H264)
                        //{
                        //    // 手动写入 Start Code
                        //    buffer[0] = 0; buffer[1] = 0; buffer[2] = 0; buffer[3] = 1;
                        //    Buffer.MemoryCopy(srcPtr, buffer + 4, frameData.Length, frameData.Length);
                        //}
                        if (!hasStartCode && (_format == VideoFormat.H264 || _format == VideoFormat.H265)) // [修改] 包含 H265
                        {
                            // 手动写入 Start Code
                            buffer[0] = 0; buffer[1] = 0; buffer[2] = 0; buffer[3] = 1;
                            Buffer.MemoryCopy(srcPtr, buffer + 4, frameData.Length, frameData.Length);
                        }
                        else
                        {
                            Buffer.MemoryCopy(srcPtr, buffer, frameData.Length, frameData.Length);
                        }
                    }

                    // 填充 Packet
                    _packet->data = buffer;
                    _packet->size = allocSize;

                    try 
                    {
                        int sendRet = ffmpeg.avcodec_send_packet(_codecContext, _packet);
                        if (sendRet < 0) return null;

                        int recvRet = ffmpeg.avcodec_receive_frame(_codecContext, _frame);

                        if (recvRet == 0)
                        {
                            // 成功解码一帧，直接转换并写入 Bitmap
                            return ConvertFrameToBitmapFast();
                        }
                        // EAGAIN 意味着需要更多输入数据才能输出帧（正常情况）
                    }
                    finally
                    {
                        // 必须释放 av_malloc 的内存，因为 packet 已经使用完毕
                        // 注意：av_packet_unref 不会释放我们自己 av_malloc 的 buffer，除非使用 av_buffer_create 包装
                        // 这里我们手动设置了 data，所以手动释放是安全的，只要在 send_packet 之后
                        ffmpeg.av_free(buffer); 
                        _packet->data = null; 
                        _packet->size = 0;
                    }

                    return null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 【核心优化】直接将 YUV 转换为 Bitmap，不经过中间的 RGB Frame 和 byte[]
        /// </summary>
        private Bitmap ConvertFrameToBitmapFast()
        {
            int srcW = _frame->width;
            int srcH = _frame->height;
            
            // 如果解码出来的尺寸发生变化（例如流切换分辨率），需要适应
            if (srcW <= 0 || srcH <= 0) return null;

            // 创建目标 Bitmap
            // PixelFormat.Format24bppRgb 在 Windows GDI+ 中通常是 BGR 顺序
            Bitmap bitmap = new Bitmap(_width, _height, PixelFormat.Format24bppRgb);
            
            BitmapData bmpData = bitmap.LockBits(
                new Rectangle(0, 0, _width, _height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb
            );

            try
            {
                // 初始化或更新 SWS Context
                // 只有当尺寸或格式改变时才重建
                if (_swsContext == null || 
                    _codecContext->width != srcW || // 检查是否有变动
                    _codecContext->height != srcH) 
                {
                    if (_swsContext != null) ffmpeg.sws_freeContext(_swsContext);

                    // SWS_BICUBIC 质量比 BILINEAR 好，虽然慢一点点，但现在 CPU 足够快
                    // 如果追求极致速度，改回 SWS_FAST_BILINEAR
                    _swsContext = ffmpeg.sws_getContext( 
                        srcW, srcH, (AVPixelFormat)_frame->format,
                        _width, _height, AVPixelFormat.AV_PIX_FMT_BGR24, // 对应 C# Format24bppRgb
                        ffmpeg.SWS_BICUBIC, null, null, null
                    );
                }
                // 准备目标数据指针数组
                // bmpData.Scan0 是 Bitmap 的内存首地址
                // bmpData.Stride 是 这一行的字节跨度 (包含 Padding)
                byte*[] dstData = { (byte*)bmpData.Scan0 };
                int[] dstLinesize = { bmpData.Stride };

                // 【Direct Rendering】
                // 直接让 sws_scale 把结果写到 Bitmap 的内存里
                ffmpeg.sws_scale(
                    _swsContext,
                    _frame->data,
                    _frame->linesize,
                    0,
                    srcH,
                    dstData,     // 直接传入 Bitmap 指针
                    dstLinesize  // 直接传入 Bitmap Stride
                );
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            return bitmap;
        }

        private void Cleanup()
        {
            if (_swsContext != null)
            {
                ffmpeg.sws_freeContext(_swsContext);
                _swsContext = null;
            }

            if (_packet != null)
            {
                AVPacket* p = _packet;
                ffmpeg.av_packet_free(&p);
                _packet = null;
            }

            if (_frame != null)
            {
                AVFrame* f = _frame;
                ffmpeg.av_frame_free(&f);
                _frame = null;
            }

            if (_codecContext != null)
            {
                AVCodecContext* c = _codecContext;
                ffmpeg.avcodec_free_context(&c);
                _codecContext = null;
            }
            
            _isInitialized = false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    Cleanup();
                }
                _disposed = true;
            }
        }

        ~FFmpegDecoder()
        {
            Dispose(false);
        }
    }
}