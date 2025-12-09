using System;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;

public class MjpegDecoder
{
    private HttpWebRequest request;
    private Stream stream;
    private Thread decodeThread;
    private bool isRunning = false;
    private byte[] latestFrame = null;
    private readonly object frameLock = new object();

    public bool IsRunning => isRunning;

    public void Connect(string url)
    {
        if (isRunning) return;

        isRunning = true;
        decodeThread = new Thread(() => DecodeStream(url)) { IsBackground = true };
        decodeThread.Start();
    }

    public void Disconnect()
    {
        isRunning = false;
        try
        {
            stream?.Close();
            stream?.Dispose();
        }
        catch { }

        if (decodeThread != null && decodeThread.IsAlive)
        {
            decodeThread.Join(1000);
        }

        lock (frameLock)
        {
            latestFrame = null;
        }
    }

    public byte[] GetNextFrame()
    {
        lock (frameLock)
        {
            return latestFrame;
        }
    }

    private void DecodeStream(string url)
    {
        try
        {
            request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;

            using (var response = request.GetResponse())
            using (stream = response.GetResponseStream())
            {
                if (stream == null)
                {
                    Debug.LogError("无法获取流");
                    isRunning = false;
                    return;
                }

                while (isRunning)
                {
                    byte[] frame = ReadJpegFrame(stream);
                    if (frame != null)
                    {
                        lock (frameLock)
                        {
                            latestFrame = frame;
                        }
                    }
                    else break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"解码错误: {e.Message}");
        }
        finally
        {
            isRunning = false;
        }
    }

    private byte[] ReadJpegFrame(Stream stream)
    {
        if (stream == null) return null;

        using (MemoryStream ms = new MemoryStream())
        {
            byte[] buffer = new byte[4096];
            bool foundFF = false;

            try
            {
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) return null;

                    for (int i = 0; i < bytesRead; i++)
                    {
                        byte b = buffer[i];
                        ms.WriteByte(b);

                        if (foundFF && b == 0xD9)
                        {
                            return ms.ToArray();
                        }

                        foundFF = (b == 0xFF);
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
