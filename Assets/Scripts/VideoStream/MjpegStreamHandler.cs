using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VideoStream
{
    /// <summary>
    /// MJPEG流自定义下载处理器
    /// 继承DownloadHandlerScript，实现流式解析MJPEG数据
    /// 解析边界标记（--boundarydonotcross），提取完整JPEG帧
    /// </summary>
    public class MjpegStreamHandler : DownloadHandlerScript
    {
        #region 常量定义

        // MJPEG流的边界标记
        private static readonly byte[] BOUNDARY = Encoding.UTF8.GetBytes("--boundarydonotcross");

        // JPEG文件格式标记
        private static readonly byte[] JPEG_START = new byte[] { 0xFF, 0xD8 }; // JPEG起始标记
        private static readonly byte[] JPEG_END = new byte[] { 0xFF, 0xD9 };   // JPEG结束标记

        // 缓冲区大小（1MB）
        private const int BUFFER_SIZE = 1024 * 1024;

        #endregion

        #region 事件定义

        /// <summary>
        /// 当接收到完整的JPEG帧时触发
        /// </summary>
        public event Action<byte[]> OnFrameReceived;

        /// <summary>
        /// 当发生错误时触发
        /// </summary>
        public event Action<string> OnError;

        #endregion

        #region 私有字段

        // 数据缓冲区
        private byte[] buffer;
        private int bufferPosition = 0;

        // 统计信息
        private int totalFrames = 0;
        private int totalBytes = 0;

        // 日志开关
        private bool enableDebugLog = false;
        
        // 目标帧率（用于优化处理）
        private int targetFrameRate = 30;

        #endregion

        #region 属性

        /// <summary>
        /// 接收到的总帧数
        /// </summary>
        public int TotalFrames => totalFrames;

        /// <summary>
        /// 接收到的总字节数
        /// </summary>
        public int TotalBytes => totalBytes;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数：初始化缓冲区
        /// </summary>
        /// <param name="enableLog">是否启用调试日志</param>
        /// <param name="targetFps">目标帧率，用于优化处理</param>
        public MjpegStreamHandler(bool enableLog = false, int targetFps = 30) : base(new byte[BUFFER_SIZE])
        {
            // 预分配最大缓冲区（4MB），避免运行时扩容触发GC卡顿
            buffer = new byte[BUFFER_SIZE * 4];
            bufferPosition = 0;
            enableDebugLog = enableLog;
            targetFrameRate = targetFps;

            if (enableDebugLog)
            {
                Debug.Log($"[MjpegStreamHandler] 初始化完成，缓冲区大小: {BUFFER_SIZE * 4}（预分配），目标帧率: {targetFps}");
            }
        }

        #endregion

        #region DownloadHandlerScript重写方法

        /// <summary>
        /// 接收数据流的核心方法
        /// 每次网络接收到数据块时被调用
        /// </summary>
        /// <param name="data">接收到的数据</param>
        /// <param name="dataLength">数据长度</param>
        /// <returns>true=继续接收，false=中断连接</returns>
        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            try
            {
                totalBytes += dataLength;

                // 检查缓冲区空间
                if (bufferPosition + dataLength > buffer.Length)
                {
                    // 缓冲区空间不足，清理已处理的数据
                    CompactBuffer();

                    // 注释掉扩容逻辑，因为已经预分配了最大空间（4MB）
                    // if (!CompactBuffer())
                    // {
                    //     // 如果缓冲区使用率不高，先尝试清理
                    //     if (bufferPosition > BUFFER_SIZE * 0.5f)
                    //     {
                    //         CompactBuffer();
                    //     }
                    //
                    //     // 如果还是不够，且未达到最大限制，则扩大缓冲区
                    //     if (bufferPosition + dataLength > buffer.Length && buffer.Length < BUFFER_SIZE * 4)
                    //     {
                    //         ResizeBuffer(Math.Min(buffer.Length * 2, BUFFER_SIZE * 4));
                    //     }
                    // }
                }

                // 检查扩容后是否仍有足够空间
                if (bufferPosition + dataLength > buffer.Length)
                {
                    // 空间仍然不足，丢弃部分旧数据以腾出空间
                    int bytesToDrop = (bufferPosition + dataLength) - buffer.Length;
                    if (bytesToDrop < bufferPosition)
                    {
                        ShiftBuffer(bytesToDrop);
                    }
                }

                // 将新数据添加到缓冲区
                Array.Copy(data, 0, buffer, bufferPosition, Math.Min(dataLength, buffer.Length - bufferPosition));
                bufferPosition += Math.Min(dataLength, buffer.Length - bufferPosition);

                // 解析缓冲区，提取完整的JPEG帧
                ParseBuffer();

                // 减少日志输出频率，避免影响性能
                if (enableDebugLog && totalFrames % 300 == 0) // 每300帧输出一次日志
                {
                    Debug.Log($"[MjpegStreamHandler] 已接收 {totalFrames} 帧，{totalBytes} 字节，缓冲区使用: {bufferPosition}/{buffer.Length}");
                }

                return true; // 继续接收数据
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MjpegStreamHandler] 接收数据时发生错误: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return false; // 中断连接
            }
        }

        /// <summary>
        /// 当流结束时调用
        /// </summary>
        protected override void CompleteContent()
        {
            base.CompleteContent();

            if (enableDebugLog)
            {
                Debug.Log($"[MjpegStreamHandler] 流结束，共接收 {totalFrames} 帧，{totalBytes} 字节");
            }
        }

        /// <summary>
        /// 接收Content-Length头时调用（MJPEG流通常没有此头）
        /// </summary>
        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            base.ReceiveContentLengthHeader(contentLength);

            if (enableDebugLog)
            {
                Debug.Log($"[MjpegStreamHandler] Content-Length: {contentLength}");
            }
        }

        #endregion

        #region 缓冲区管理

        /// <summary>
        /// 解析缓冲区，提取完整的JPEG帧
        /// </summary>
        private void ParseBuffer()
        {
            int searchStart = 0;
            int processedFrames = 0;
            // 根据目标帧率调整每次处理的帧数，避免阻塞主线程
            int maxFramesPerCall = Mathf.Clamp(targetFrameRate / 10, 2, 10);

            // 循环查找所有完整的JPEG帧
            while (searchStart < bufferPosition && processedFrames < maxFramesPerCall)
            {
                // 1. 查找JPEG起始标记 (0xFF 0xD8)
                int jpegStartIndex = FindPattern(buffer, JPEG_START, searchStart, bufferPosition);

                if (jpegStartIndex == -1)
                {
                    // 未找到起始标记，保留从searchStart开始的数据
                    break;
                }

                // 2. 查找JPEG结束标记 (0xFF 0xD9)
                int jpegEndIndex = FindPattern(buffer, JPEG_END, jpegStartIndex + 2, bufferPosition);

                if (jpegEndIndex == -1)
                {
                    // 未找到结束标记，说明帧不完整，保留从jpegStartIndex开始的数据
                    if (jpegStartIndex > 0)
                    {
                        ShiftBuffer(jpegStartIndex);
                    }
                    break;
                }

                // 3. 提取完整的JPEG帧（包含结束标记）
                int frameLength = jpegEndIndex - jpegStartIndex + 2;
                byte[] frame = new byte[frameLength];
                Array.Copy(buffer, jpegStartIndex, frame, 0, frameLength);

                // 4. 触发帧接收事件
                EmitFrame(frame);
                processedFrames++;

                // 5. 更新搜索起点（跳过已处理的帧）
                searchStart = jpegEndIndex + 2;
                
                // 如果处理的数据过多，强制清理缓冲区以防止内存无限增长
                if (searchStart > 100000) // 100KB阈值
                {
                    ShiftBuffer(searchStart);
                    searchStart = 0;
                }
            }

            // 移除已处理的数据
            if (searchStart > 0)
            {
                ShiftBuffer(searchStart);
            }
            
            // 如果缓冲区过大，强制清理
            if (bufferPosition > BUFFER_SIZE * 0.8f) 
            {
                CompactBuffer();
            }
        }

        /// <summary>
        /// 在字节数组中查找模式
        /// </summary>
        /// <param name="source">源数组</param>
        /// <param name="pattern">要查找的模式</param>
        /// <param name="start">起始位置</param>
        /// <param name="end">结束位置</param>
        /// <returns>找到的位置，未找到返回-1</returns>
        private int FindPattern(byte[] source, byte[] pattern, int start, int end)
        {
            int patternLength = pattern.Length;
            int maxIndex = end - patternLength;

            for (int i = start; i <= maxIndex; i++)
            {
                bool found = true;
                for (int j = 0; j < patternLength; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 移除缓冲区前面的数据，保留后面的数据
        /// </summary>
        /// <param name="fromIndex">从此索引开始保留数据</param>
        private void ShiftBuffer(int fromIndex)
        {
            if (fromIndex >= bufferPosition)
            {
                // 所有数据都被处理，清空缓冲区
                bufferPosition = 0;
                return;
            }

            int remainingLength = bufferPosition - fromIndex;
            Array.Copy(buffer, fromIndex, buffer, 0, remainingLength);
            bufferPosition = remainingLength;
        }

        /// <summary>
        /// 压缩缓冲区（移除已处理数据）
        /// </summary>
        /// <returns>true=成功压缩，false=无法压缩</returns>
        private bool CompactBuffer()
        {
            // 查找第一个JPEG起始标记
            int firstJpegStart = FindPattern(buffer, JPEG_START, 0, bufferPosition);

            if (firstJpegStart > 0)
            {
                // 移除起始标记之前的数据
                ShiftBuffer(firstJpegStart);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 扩大缓冲区容量
        /// </summary>
        /// <param name="newSize">新大小</param>
        private void ResizeBuffer(int newSize)
        {
            byte[] newBuffer = new byte[newSize];
            Array.Copy(buffer, 0, newBuffer, 0, bufferPosition);
            buffer = newBuffer;

            if (enableDebugLog)
            {
                Debug.LogWarning($"[MjpegStreamHandler] 缓冲区扩容至: {newSize} 字节");
            }
        }

        #endregion

        #region 帧处理

        /// <summary>
        /// 触发帧接收事件
        /// </summary>
        /// <param name="frame">JPEG帧数据</param>
        private void EmitFrame(byte[] frame)
        {
            totalFrames++;

            if (enableDebugLog && totalFrames % 30 == 0) // 每30帧打印一次日志
            {
                Debug.Log($"[MjpegStreamHandler] 接收第 {totalFrames} 帧，大小: {frame.Length} 字节");
            }

            try
            {
                OnFrameReceived?.Invoke(frame);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MjpegStreamHandler] 帧处理回调发生错误: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void ResetStatistics()
        {
            totalFrames = 0;
            totalBytes = 0;
        }

        #endregion
    }
}
