using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using System;

public class KinectStreamingListener {

	public static int MaxLinesPerBlock = 16;
	public static int LineWidth = 512;
	public static int TextureHeight = 424;

	Thread listenThread;
	Thread processThread;
	Queue<KinectFrame> messageQueue;
	Queue<KinectFrame> processQueue;
	Queue<KinectFrame> unusedQueue;

	public GameObject cameraTransform;
	private Vector3 cameraPos = new Vector3();
	private Quaternion cameraRot = new Quaternion();

	KinectFrame[] kinectFrameBuffer;

	private int headerSize = 10;

	UdpClient udpClient;
	private object _messageQueueLock = new object();
	private object _processQueueLock = new object();
	private object _unusedQueueLock = new object();

	bool listening;
	bool processing;
	int port;

	UInt32 newestSequence = 0;

	public KinectStreamingListener(int port) {
		kinectFrameBuffer = new KinectFrame[64];
		unusedQueue = new Queue<KinectFrame> ();
		for (int i = 0; i < kinectFrameBuffer.Length; i++) {
			kinectFrameBuffer [i] = new KinectFrame ();
			unusedQueue.Enqueue (kinectFrameBuffer [i]);
		}
		messageQueue = new Queue<KinectFrame> ();
		processQueue = new Queue<KinectFrame> ();

		ThreadStart listenStart = new ThreadStart (Listen);
		listenThread = new Thread(listenStart);
		this.port = port;
		listenThread.Start();

		ThreadStart processStart = new ThreadStart (Process);
		processThread = new Thread(processStart);
		processThread.Start();
	}

	public void Close() {
		listening = false;
		processing = false;
		if (udpClient != null)
			udpClient.Close();
		if (listenThread != null)
			listenThread.Join (500);
		if (processThread != null)
			processThread.Join (500);
	}

	public void Release(KinectFrame frame) {
		lock (_unusedQueueLock) {
			unusedQueue.Enqueue (frame);
		}
	}

	private int QueuedProcesses() {
		lock (_processQueueLock) {
			return processQueue.Count;
		}
	}

	private KinectFrame ReadProcess() {
		lock (_processQueueLock) {
			return processQueue.Dequeue();
		}
	}

	public int QueuedMessages() {
		lock (_messageQueueLock) {
			return messageQueue.Count;
		}
	}

	public KinectFrame Read() {
		lock (_messageQueueLock) {
			return messageQueue.Dequeue();
		}
	}

	void Process() {
		processing = true;
		try {
			while (processing) {
				if (QueuedProcesses() < 1) continue;

				KinectFrame frame = ReadProcess();
				//frame.colorDataC = new Color[512 * frame.lines];
				/*
				for (int pixel = 0; pixel < frame.colorDataC.Length; pixel++) {
					frame.colorDataC [pixel] = new Color (
						frame.colorData [pixel * 4 + 2] / 255.0f,
						frame.colorData [pixel * 4 + 1] / 255.0f,
						frame.colorData [pixel * 4 + 0] / 255.0f
					);
				}*/

				for (int pixel = 0; pixel < frame.depthData16.Length; pixel++) {
					frame.depthData16[pixel] = System.BitConverter.ToUInt16 (frame.depthData, pixel * 2);
				}

				/*
				frame.depthDataC = new Color[512 * frame.lines];
				for (int pixel = 0; pixel < frame.depthDataC.Length; pixel++) {
					ushort value = System.BitConverter.ToUInt16 (frame.depthData, pixel * 2);

					frame.depthDataC [pixel] = new Color (
						value / 2000.0f,
						value / 2000.0f,
						value / 2000.0f
					);
				}*/

				lock (_messageQueueLock) {
					messageQueue.Enqueue(frame);
				}
			}
		} catch (Exception e) {
			Debug.Log (e);
		} finally {
			listening = false;
			processing = false;
		}
		Debug.Log ("Process Thread Closed");
	}

	void Listen() {
		try {
			udpClient = new UdpClient(port);
			listening = true;
			while (listening) {
				try {
					IPEndPoint receiveEndPoint = new IPEndPoint(IPAddress.Any, port);
					byte[] receiveBytes = udpClient.Receive(ref receiveEndPoint);
					if (receiveBytes[0]  == 0x03) {
						if (unusedQueue.Count == 0) {
							Debug.Log("Skipped frame bc no unused buffers available");
							continue;
						}

						KinectFrame frame = unusedQueue.Dequeue();
						frame.deviceID = receiveBytes[1];

						frame.sequence = BitConverter.ToUInt32(receiveBytes, 2);
						frame.startRow = BitConverter.ToUInt16(receiveBytes, 6);
						frame.endRow = BitConverter.ToUInt16(receiveBytes, 8);

						frame.lines = frame.endRow-frame.startRow;
						int depthDataSize = frame.lines*(KinectStreamingListener.LineWidth*2);
						int colorDataSize = frame.lines*(KinectStreamingListener.LineWidth/2); //*4

						/*
						Debug.Log("Lines: "+frame.lines);
						Debug.Log("Data in:  "+receiveBytes.Length);
						Debug.Log("Header Size: "+headerSize);
						Debug.Log("Depth Size: "+depthDataSize);
						Debug.Log("Color Size: "+colorDataSize);
						*/

						if (newestSequence < frame.sequence) {
							newestSequence = frame.sequence;
							/*
							if (processQueue.Count > 0) {
								Debug.Log(processQueue.Count+" unprocessed dropped, seq: "+frame.sequence);
								lock (_processQueueLock) lock (_unusedQueueLock) {
									while (processQueue.Count > 0) {
										unusedQueue.Enqueue(processQueue.Dequeue());
									}
								}
							}*/

							if (messageQueue.Count > 0) {
								//Debug.Log(messageQueue.Count+" unrendered dropped, seq: "+frame.sequence);
								lock (_messageQueueLock) lock (_unusedQueueLock) {
									while (messageQueue.Count > 0) {
										unusedQueue.Enqueue(messageQueue.Dequeue());
									}
								}
							}
						}

						Buffer.BlockCopy(receiveBytes, headerSize, frame.depthData, 0, depthDataSize);
						Buffer.BlockCopy(receiveBytes, headerSize+depthDataSize, frame.colorData, 0, colorDataSize);

						lock (_processQueueLock) {
							processQueue.Enqueue(frame);
						}
					}

				} catch (Exception e) {
					Debug.Log (e);
				}
			}
		} catch (Exception e) {
			Debug.Log (e);
		} finally {
			udpClient.Close();
			listening = false;
			processing = false;
		}

		Debug.Log ("Listen Thread Closed");
	}

}

public class KinectFrame {
	public int deviceID = 0;
	public int startRow = 0;
	public int endRow = 0;
	public int lines = 0;
	public UInt32 sequence = 0;
	public byte[]   depthData = new byte[KinectStreamingListener.MaxLinesPerBlock*KinectStreamingListener.LineWidth*2];
	public byte[]   colorData = new byte[KinectStreamingListener.MaxLinesPerBlock*KinectStreamingListener.LineWidth/2];
	public Color[] colorDataC = new Color[KinectStreamingListener.MaxLinesPerBlock*KinectStreamingListener.LineWidth];
	public ushort[] depthData16 = new ushort[KinectStreamingListener.MaxLinesPerBlock*KinectStreamingListener.LineWidth];
	//public Color[] depthDataC = new Color[KinectStreamingListener.LinesPerBlock*512];
}