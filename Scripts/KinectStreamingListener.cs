using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using System;


public class KinectFrame
{
    public int deviceID = 0;
    public int startRow = 0;
    public int endRow = 0;
    public int lines = 0;
    public UInt32 sequence = 0;
    public byte[] depthData = new byte[KinectStreamingListener.MaxLinesPerBlock * KinectStreamingListener.LineWidth * 2];
    public byte[] colorData = new byte[KinectStreamingListener.MaxLinesPerBlock * KinectStreamingListener.LineWidth / 2];
}

public class CounterFrame
{
    public int count = 0;
    public float timeStamp;
    public bool printed;

}


public class KinectStreamingListener
{

    public const int MaxLinesPerBlock = 40;
    public const int LineWidth = 512;
    public const int TextureHeight = 424;

    public float dropsPerSecond = 0.0f;
    public Dictionary<uint, CounterFrame> frameCounters = new Dictionary<uint, CounterFrame>();
    public object frameCountersLock = new object();

    private int dropCounter = 0;
    private float lastDropAverage = 0.0f;

    Thread listenThread;
    Thread processThread;
    Queue<KinectFrame> processQueue;
    Queue<KinectFrame> unusedQueue;

    KinectFrame[] kinectFrameBuffer;

    private int headerSize = 10;

    UdpClient udpClient;
    private object _processQueueLock = new object();
    private object _unusedQueueLock = new object();


    private object _depthDataLock = new object();
    private object _colorDataLock = new object();

    private object _depthResLock = new object();

    bool listening;
    bool processing;
    int port;

    UInt32 newestSequence = 0;

    public ushort[] _DepthData;
    private byte[] _ColorData;

    private Color[] _DepthRes;

    public KinectStreamingListener(int port)
    {
        kinectFrameBuffer = new KinectFrame[32];

        _DepthData = new ushort[LineWidth * TextureHeight];
        _ColorData = new byte[TextureHeight * (LineWidth / 2)];
        _DepthRes = new Color[KinectStreamingListener.TextureHeight * KinectStreamingListener.LineWidth];

        unusedQueue = new Queue<KinectFrame>();
        for (int i = 0; i < kinectFrameBuffer.Length; i++)
        {
            kinectFrameBuffer[i] = new KinectFrame();
            unusedQueue.Enqueue(kinectFrameBuffer[i]);
        }
        processQueue = new Queue<KinectFrame>();

        ThreadStart listenStart = new ThreadStart(Listen);
        listenThread = new Thread(listenStart);
        this.port = port;
        listenThread.Start();

        ThreadStart processStart = new ThreadStart(Process);
        processThread = new Thread(processStart);
        processThread.Start();
    }

    public void ColorLoadRaw(ref Texture2D tex)
    {
        lock (_colorDataLock)
        {
            tex.LoadRawTextureData(_ColorData);
            tex.Apply();
        }
    }

    public byte[] GetRawColorData()
    {
        lock (_colorDataLock)
        {
            byte[] returnBytes = new byte[_ColorData.Length];
            Array.Copy(_ColorData, returnBytes, _ColorData.Length);
            return returnBytes;
        }
    }


    public Color[] ComputeDepthColors()
    {
        _ComputeDepthColors();
        return _DepthRes;
        /*
		lock (_depthResLock) {
			return _DepthRes;
		}*/
    }

    private void _ComputeDepthColors()
    {
        for (int y = 0; y < TextureHeight; y++)
        {
            for (int x = 0; x < LineWidth; x++)
            {
                int fullIndex = (y * LineWidth) + x;

                float zc = 0.0f;
                lock (_depthResLock)
                {
                    zc = 71 * _DepthData[fullIndex] / 65535F;
                }
                float xc = 1 - (x / (float)LineWidth) - 0.5F;
                float yc = 1 - (y / (float)TextureHeight) - 0.5F;

                xc *= zc * (LineWidth / (float)TextureHeight);
                yc *= zc;

                _DepthRes[fullIndex] = new Color(xc, yc, zc);
            }
        }
    }

    public void Close()
    {
        listening = false;
        processing = false;
        if (udpClient != null)
            udpClient.Close();
        if (listenThread != null)
            listenThread.Join(500);
        if (processThread != null)
            processThread.Join(500);
    }

    private int QueuedProcesses()
    {
        lock (_processQueueLock)
        {
            return processQueue.Count;
        }
    }

    private KinectFrame PollProcess()
    {
        lock (_processQueueLock)
        {
            return processQueue.Dequeue();
        }
    }
    
    private KinectFrame PollUnused()
    {
        lock (_unusedQueueLock)
        {
            return unusedQueue.Dequeue();
        }
    }


    private void Process()
    {
        processing = true;
        DateTime start = DateTime.UtcNow;
        try
        {
            while (processing)
            {
                float now = (float)(DateTime.UtcNow.Subtract(start)).TotalSeconds;
                if (lastDropAverage + 3.0 <= now)
                {
                    lastDropAverage = now;
                    dropsPerSecond = (float)dropCounter / 3.0f;
                    dropCounter = 0;
                }

                if (QueuedProcesses() < 1) continue;

                KinectFrame frame = PollProcess();

                if (newestSequence < frame.sequence)
                {
                    newestSequence = frame.sequence;

                    if (processQueue.Count > 0)
                    {
                        dropCounter = dropCounter + processQueue.Count;
                        //Debug.Log(dropCounter+" unprocessed dropped, seq: "+frame.sequence);

                        lock (_processQueueLock) lock (_unusedQueueLock)
                            {
                                while (processQueue.Count > 0)
                                {
                                    unusedQueue.Enqueue(processQueue.Dequeue());
                                }
                            }
                    }

                    /* 
					//Alternative: compute depth colors in thread
					lock (_depthResLock) {
						_ComputeDepthColors();
					} */
                }

                // Part of old frame (could still happen, even if we drop old data).
                if (frame.sequence < newestSequence)
                {
                    lock (_unusedQueueLock)
                    {
                        unusedQueue.Enqueue(frame);
                    }
                    continue;
                }

                // Color Data
                lock (_colorDataLock)
                {
                    Buffer.BlockCopy(frame.colorData, 0, _ColorData, frame.startRow * LineWidth / 2, frame.lines * LineWidth / 2);
                }

                // TODO: in between these two, render thread could get a depth image that is behind the color image...
                // ...lock on both instead?

                // Depth Data
                int writeOffset = frame.startRow * LineWidth;
                int pixelsToWrite = frame.lines * LineWidth;
                lock (_depthDataLock)
                {
                    for (int pixel = 0; pixel < pixelsToWrite; pixel++)
                    {
                        _DepthData[writeOffset + pixel] = System.BitConverter.ToUInt16(frame.depthData, pixel * 2);
                    }
                }

                lock (_unusedQueueLock)
                {
                    unusedQueue.Enqueue(frame);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        finally
        {
            listening = false;
            processing = false;
        }
        Debug.Log("Process Thread Closed");
    }

    private void Listen()
    {
        try
        {
            udpClient = new UdpClient(port);
            listening = true;
            while (listening)
            {
                try
                {
                    IPEndPoint receiveEndPoint = new IPEndPoint(IPAddress.Any, port);
                    byte[] receiveBytes = udpClient.Receive(ref receiveEndPoint);
                    if (receiveBytes[0] == 0x03)
                    {
                        if (unusedQueue.Count == 0)
                        {
                            Debug.Log("Skipped frame bc no unused buffers available");
                            continue;
                        }

                        KinectFrame frame = PollUnused();
                        frame.deviceID = receiveBytes[1];

                        frame.sequence = BitConverter.ToUInt32(receiveBytes, 2);
                        frame.startRow = BitConverter.ToUInt16(receiveBytes, 6);
                        frame.endRow = BitConverter.ToUInt16(receiveBytes, 8);

                        frame.lines = frame.endRow - frame.startRow;
                        int depthDataSize = frame.lines * (KinectStreamingListener.LineWidth * 2);
                        int colorDataSize = frame.lines * (KinectStreamingListener.LineWidth / 2); //*4

                        Buffer.BlockCopy(receiveBytes, headerSize, frame.depthData, 0, depthDataSize);
                        Buffer.BlockCopy(receiveBytes, headerSize + depthDataSize, frame.colorData, 0, colorDataSize);

                        //lock (_unusedQueueLock)
                        //{
                        //    unusedQueue.Enqueue(frame);
                        //}

                        lock (frameCountersLock)
                        {
                            if (frameCounters.ContainsKey(frame.sequence))
                            {
                                CounterFrame newCounter = frameCounters[frame.sequence];
                                newCounter.count++;
                            }
                            else
                            {
                                CounterFrame newCounter = new CounterFrame();
                                newCounter.count = 1;
                                newCounter.printed = false;
                                newCounter.timeStamp = (float)(DateTime.Now.ToUniversalTime() - new DateTime(2017, 7, 16)).TotalSeconds;
                                frameCounters.Add(frame.sequence, newCounter);
                            }
                        }

                        lock (_processQueueLock)
                        {
                            processQueue.Enqueue(frame);
                        }
                    }

                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        finally
        {
            udpClient.Close();
            listening = false;
            processing = false;
        }

        Debug.Log("Listen Thread Closed");
    }

}