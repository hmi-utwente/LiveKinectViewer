using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;

namespace HMIMR.DepthStreaming {

    public class DepthBlock {
        public ushort StartRow { get; private set; }
        public ushort EndRow { get; private set; }
        public ushort Lines { get; private set; }
        public UInt32 Sequence { get; private set; }

        public byte[] DepthData { get; private set; }
        public byte[] ColorData { get; private set; }

        private readonly ushort _totalWidth;

        public DepthBlock(ushort maxLinesPerBlock, ushort totalWidth) {
            DepthData = new byte[maxLinesPerBlock * totalWidth * 2];
            ColorData = new byte[maxLinesPerBlock * totalWidth / 2];
            _totalWidth = totalWidth;
        }

        public void LoadData(ushort sr, ushort er, UInt32 seq, ref byte[] data, int dataOffset) {
            StartRow = sr;
            EndRow = er;
            Lines = (ushort) (er - sr);
            Sequence = seq;

            int depthDataSize = Lines * (_totalWidth * 2);
            int colorDataSize = Lines * (_totalWidth / 2);

            Buffer.BlockCopy(data, dataOffset, DepthData, 0, depthDataSize);
            Buffer.BlockCopy(data, dataOffset + depthDataSize, ColorData, 0, colorDataSize);
        }
    }

    public class DefaultDepthStreamingProcessor : DepthStreamingProcessor {
        private volatile ushort[] _depthData;
        private volatile byte[] _colorData;
        private readonly Color[] _depthResult;

        private readonly int depthBlockBufferSize = 64;
        private readonly Queue<DepthBlock> _unusedQueue;
        private readonly Queue<DepthBlock> _processQueue;
        private readonly object _unusedQueueLock = new object();
        private readonly object _processQueueLock = new object();
        private readonly object _colorDataLock = new object();
        private readonly object _depthDataLock = new object();
        private readonly object _depthDataResLock = new object();
        private readonly Thread _processThread;

        private bool _processing;
        private UInt32 _newestSequence = 0;

        public DefaultDepthStreamingProcessor(DepthStreamingSource fs, DepthDeviceType t, DepthCameraIntrinsics cI,
            ushort w, ushort h, ushort ml, string guid)
            : base(fs, t, cI, w, h, ml, guid) {
            _depthResult = new Color[TotalWidth * TotalWidth];
            _depthData = new ushort[TotalWidth * TotalHeight];
            _colorData = new byte[TotalHeight * TotalWidth / 2];
            _processQueue = new Queue<DepthBlock>();
            _unusedQueue = new Queue<DepthBlock>();
            for (int i = 0; i < depthBlockBufferSize; i++) {
                _unusedQueue.Enqueue(new DepthBlock(MaxLinesPerBlock, TotalWidth));
            }

            _processThread = new Thread(new ThreadStart(Process));
            _processThread.Start();
        }

        public byte[] GetRawColorData() {
            lock (_colorDataLock)
                return _colorData;
        }

        public ushort[] GetRawDepthData() {
            //UpdateDepthResult();
            lock (_depthDataLock)
                return _depthData;
        }

        public Color[] GetDepthData() {
            lock (_depthDataResLock)
                return _depthResult;
        }

        public override void Close() {
            _processing = false;
            if (_processThread != null)
                _processThread.Join(1000);
        }

        private void Process() {
            _processing = true;
            DateTime start = DateTime.UtcNow;
            try {
                while (_processing) {
                    //float now = (float) (DateTime.UtcNow.Subtract(start)).TotalSeconds;
                    DepthBlock block;
                    lock (_processQueueLock) {
                        if (_processQueue.Count < 1) continue;
                        block = _processQueue.Dequeue();
                    }

                    if (_newestSequence < block.Sequence) {
                        PreFrameObj newFrame = new PreFrameObj();
                        newFrame.DXT1_colors =
                            GetRawColorData(); // Sketchy, assuming every implementation makes DXT1 colors
                        newFrame.colSize = new Vector2(TotalWidth, TotalHeight);
                        newFrame.positions = GetDepthData();
                        newFrame.posSize = new Vector2(TotalWidth, TotalHeight);
                        newFrame.cameraPos = FrameSource.cameraPosition;
                        newFrame.cameraRot = FrameSource.cameraRotation;
                        FrameSource.frameQueue.Enqueue(newFrame);

                        _newestSequence = block.Sequence;

                        if (_processQueue.Count > 0) {
                            lock (_processQueueLock)
                            lock (_unusedQueueLock) {
                                while (_processQueue.Count > 0) {
                                    _unusedQueue.Enqueue(_processQueue.Dequeue());
                                }
                            }
                        }
                    }

                    // Part of old frame (could still happen, even if we drop old data).
                    if (block.Sequence < _newestSequence) {
                        lock (_unusedQueueLock) {
                            _unusedQueue.Enqueue(block);
                        }
                        continue;
                    }

                    // Color & Depth Data
                    lock (_colorDataLock)
                    lock (_depthDataLock) {
                        Buffer.BlockCopy(block.ColorData, 0, _colorData, block.StartRow * TotalWidth / 2,
                            block.Lines * TotalWidth / 2);
                        Buffer.BlockCopy(block.DepthData, 0, _depthData, block.StartRow * TotalWidth * 2,
                            block.Lines * TotalWidth * 2);
                        UpdateDepthResult(block.StartRow, block.EndRow); // => doing this here seems efficient
                    }

                    lock (_unusedQueueLock) {
                        _unusedQueue.Enqueue(block);
                    }
                }
            } catch (Exception e) {
                Debug.Log(e);
            } finally {
                _processing = false;
            }

            Debug.Log("Process Thread Closed");
        }

        public override void HandleData(ushort sr, ushort er, UInt32 seq, ref byte[] data, int dataOffset) {
            DepthBlock block;
            lock (_unusedQueueLock) {
                if (_unusedQueue.Count < 1) {
                    Debug.Log("Skipped frame bc no unused buffers available");
                    return;
                }

                block = _unusedQueue.Dequeue();
            }

            block.LoadData(sr, er, seq, ref data, dataOffset);

            lock (_processQueueLock) {
                _processQueue.Enqueue(block);
            }
        }

        private void UpdateDepthResult() {
            UpdateDepthResult(0, TotalHeight);
        }

        private void UpdateDepthResult(int startRow, int endRow) {
            for (int y = startRow; y < endRow; y++) {
                for (int x = 0; x < TotalWidth; x++) {
                    int fullIndex = (y * TotalWidth) + x;
                    float zc = _depthData[fullIndex] * CameraIntrinsics.DepthScale;

                    float xc = (x - CameraIntrinsics.Cx) * zc / CameraIntrinsics.Fx;
                    float yc = -(y - CameraIntrinsics.Cy) * zc / CameraIntrinsics.Fy;
                    lock (_depthDataResLock) {
                        _depthResult[fullIndex] = new Color(xc, yc, zc);
                    }
                }
            }
        }
    }

}