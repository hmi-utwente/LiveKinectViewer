using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using System;

namespace HMIMR.DepthStreaming {

    public class DepthStreamingListener {
        public DepthStreamingProcessor processor;
        private readonly int _port;
        private bool _listening;
        private UdpClient _udpClient;
        private readonly Thread _listenThread;
        private int headerSize = 12;
        private readonly object _udpClientLock = new object();
        private readonly FrameSource _frameSource;

        public DepthStreamingListener(int port, FrameSource fs) {
            _listenThread = new Thread(new ThreadStart(Listen));
            _port = port;
            _frameSource = fs;
            _listenThread.Start();
        }

        private void Listen() {
            _listening = true;
            try {
                _udpClient = new UdpClient(_port);
                _udpClient.Client.ReceiveBufferSize = 64012*40*2; // ~2 Frames
            } catch (Exception e) {
                Debug.Log("Error Initializing Listener: " + e);
                _listening = false;
            }

            while (_listening) {
                byte[] receiveBytes = new byte[] { };
                IPEndPoint receiveEndPoint = new IPEndPoint(IPAddress.Any, _port);
                try {
                    lock (_udpClientLock) {
                        receiveBytes = _udpClient.Receive(ref receiveEndPoint);
                    }
                } catch (Exception e) {
                    _listening = false;
                    Debug.Log("Error Reading Depth Streaming Data: " + e);
                    break;
                }

                if (receiveBytes.Length < 2) continue;
                byte frameType = receiveBytes[0];
                byte deviceID = receiveBytes[1];

                switch (frameType) {
                    case (byte) FrameType.Config:
                        if (processor != null) {
                            // Currently only one processor per port.
                            // We could support here:
                            //   - Changing configuration (based on throtteling, etc)
                            //   - Multiple devices on one port (routing DepthPackets to processor based on id)
                            //   - (...which would require some changes to how streaming source & render works)
                            break;
                        }

                        // TODO: Parse config data
                        DepthDeviceType type = (DepthDeviceType) receiveBytes[2];
                        ushort frameWidth = BitConverter.ToUInt16(receiveBytes, 4);
                        ushort frameHeight = BitConverter.ToUInt16(receiveBytes, 6);
                        ushort maxLines = BitConverter.ToUInt16(receiveBytes, 8);

                        float cx = BitConverter.ToSingle(receiveBytes, 12);
                        float cy = BitConverter.ToSingle(receiveBytes, 16);
                        float fx = BitConverter.ToSingle(receiveBytes, 20);
                        float fy = BitConverter.ToSingle(receiveBytes, 24);
                        float depthScale = BitConverter.ToSingle(receiveBytes, 28);
                        DepthCameraIntrinsics cI = new DepthCameraIntrinsics(
                            cx, cy, fx, fy, depthScale);
                        string guid = "";
                        for (int sOffset = 0; sOffset < 32; sOffset++) {
                            byte c = receiveBytes[32 + sOffset];
                            if (c == 0x00) break;
                            guid += (char) c;
                        }

                        Debug.Log("Config:\n\tFrame: " + frameWidth + " " + frameHeight + " " + maxLines +
                                  "\n\tIntrinsics: " + cx + " " + cy + " " + fx + " " + fy + " " + depthScale +
                                  "\n\tGUID: " + guid);
                        // We could also implement & choose a specific Processor 
                        // (i.e. with custom Proccess() function) based on DepthDeviceType...

                        //processor = new DefaultDepthStreamingProcessor(
                        //processor = new VSyncProcessor(
                        processor = new FastProcessor(
                            _frameSource, type, cI, frameWidth, frameHeight, maxLines, guid);
                        break;
                    case (byte) FrameType.DepthBlock:
                        if (processor == null) break;
                        UInt32 sequence = BitConverter.ToUInt32(receiveBytes, 4);
                        ushort startRow = BitConverter.ToUInt16(receiveBytes, 8);
                        ushort endRow = BitConverter.ToUInt16(receiveBytes, 10);

                        //Debug.Log("Seq: "+sequence+" start: "+startRow+" end: "+endRow);
                        processor.HandleData(startRow, endRow, sequence, ref receiveBytes, headerSize);
                        break;
                    default:
                        Debug.Log("Unknown DepthStreaming frame type: " + receiveBytes[0]);
                        break;
                }
            }

            lock (_udpClientLock) {
                _udpClient.Close();
            }
            _listening = false;

            Debug.Log("Listen Thread Closed");
        }

        public void Close() {
            _listening = false;
            if (processor != null)
                processor.Close();
            if (_udpClient != null) {
                lock (_udpClientLock) {
                    _udpClient.Close();
                }
            }
            if (_listenThread != null)
                _listenThread.Join(500);
        }
    }

    public enum FrameType {
        Config = 0x01,
        DepthBlock = 0x03
    }

    public enum DepthDeviceType {
        KinectV1 = 0x01,
        KinectV2 = 0x02,
        SR200 = 0x03
    };

    public struct DepthCameraIntrinsics {
        public readonly float Cx;
        public readonly float Cy;
        public readonly float Fx;
        public readonly float Fy;
        public readonly float DepthScale;

        public DepthCameraIntrinsics(float cx, float cy, float fx, float fy, float depthScale) {
            Cx = cx;
            Cy = cy;
            Fx = fx;
            Fy = fy;
            DepthScale = depthScale;
        }
    }

}