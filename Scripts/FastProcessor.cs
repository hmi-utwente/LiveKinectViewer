using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;
using System.Linq;

namespace HMIMR.DepthStreaming {

    public class FastFrame : APreFrameObj {
        public ushort[] DepthData { get; private set; }

        private readonly DepthStreamingProcessor _processor;

        public FastFrame(DepthStreamingProcessor p) {
            _processor = p;
            DXT1_colors = null;//new byte[_processor.TotalHeight * _processor.TotalWidth / 2];
            JPEG_colors = null;// new byte[_processor.TotalHeight * _processor.TotalWidth / 2];
            DepthData = new ushort[_processor.TotalHeight * _processor.TotalWidth];
            positions = new Color[_processor.TotalHeight * _processor.TotalWidth];
            colSize = new Vector2(_processor.TotalWidth, _processor.TotalHeight);
            posSize = new Vector2(_processor.TotalWidth, _processor.TotalHeight);
        }

        public void CopyFrom(FastFrame src) {
            // assuming initialized with same values
            //Buffer.BlockCopy(src.DXT1_colors, 0, DXT1_colors, 0, DXT1_colors.Length);
            JPEG_colors = src.JPEG_colors;// (byte[]) src.JPEG_colors.Clone();
            src.positions.CopyTo(positions, 0);
        }

        public override void Release() {
            //JPEG_colors = null;
            ((FastProcessor) _processor).ReturnFromRender(this);
        }

        public void LoadColorData(ref byte[] data, int dataOffset) {
            int jpegLength = data.Length - dataOffset;
            JPEG_colors = new byte[jpegLength];
            Buffer.BlockCopy(data, dataOffset, JPEG_colors, 0, jpegLength);
        }

        public void LoadDepthData(ushort sr, ushort er, ref byte[] data, int dataOffset) {
            ushort lines = (ushort) (er - sr);
            int depthDataSize = lines * _processor.TotalWidth * 2;
            //int colorDataSize = lines * _processor.TotalWidth / 2;

            Buffer.BlockCopy(data, dataOffset, DepthData,
                sr * _processor.TotalWidth * 2, depthDataSize);
            /*
            Buffer.BlockCopy(data, dataOffset + depthDataSize, DXT1_colors,
                sr * _processor.TotalWidth / 2, colorDataSize);
                */
            ComputeDepthColors(sr, er);
        }

        public void ComputeDepthColors() {
            ComputeDepthColors(0, _processor.TotalHeight);
        }

        private void ComputeDepthColors(int startRow, int endRow) {
            for (int y = startRow; y < endRow; y++) {
                for (int x = 0; x < _processor.TotalWidth; x++) {
                    int fullIndex = (y * _processor.TotalWidth) + x;
                    float zc = DepthData[fullIndex] * _processor.CameraIntrinsics.DepthScale;
                    float xc = -(x - _processor.CameraIntrinsics.Cx) * zc / _processor.CameraIntrinsics.Fx;
                    float yc = -(y - _processor.CameraIntrinsics.Cy) * zc / _processor.CameraIntrinsics.Fy;
                    positions[fullIndex] = new Color(xc, yc, zc);
                }
            }
        }
    }

    public class FastProcessor : DepthStreamingProcessor {

        private readonly int _frameBufferSize = 30;
        private readonly Queue<FastFrame> _frameBuffer;
        private readonly object _frameBufferLock = new object();

        private bool _processing;
        private UInt32 _newestSequence = 0;
        private int continuousSmallerSeqAm = 0;

        public FastProcessor(DepthStreamingSource fs, DepthDeviceType t, DepthCameraIntrinsics cI,
            ushort w, ushort h, ushort ml, string guid)
            : base(fs, t, cI, w, h, ml, guid) {
            _frameBuffer = new Queue<FastFrame>();
            for (int i = 0; i < _frameBufferSize; i++) {
                _frameBuffer.Enqueue(new FastFrame(this));
            }
        }

        public void ReturnFromRender(FastFrame ff) {
            lock (_frameBufferLock) {
                _frameBuffer.Enqueue(ff);
            }
        }

        public override void Close() { }

        public override void HandleColorData(uint seq, ref byte[] data, int dataOffset) {
            try {
                if (seq < _newestSequence) return;
                lock (_frameBufferLock) {
                    if (_frameBuffer.Count < 2) {
                        Debug.LogWarning("Renderer not fast enough, dropping a frame.");
                        return;
                    }

                    _frameBuffer.Peek().LoadColorData(ref data, dataOffset);

                }
            } catch (Exception e) {
                Debug.LogError(e);
            }
        }

        public override void HandleDepthData(ushort sr, ushort er, UInt32 seq, ref byte[] data, int dataOffset) {
            try {
                if (seq < _newestSequence) {
                    continuousSmallerSeqAm++;
                    if (continuousSmallerSeqAm > 30)
                        _newestSequence = seq-1;
                    else
                        return;
                }
                continuousSmallerSeqAm = 0;

                FastFrame ff = null;

                lock (_frameBufferLock) {
                    if (_frameBuffer.Count < 2) {
                        Debug.LogWarning("Renderer not fast enough, dropping a frame.");
                        return;
                    }

                    _frameBuffer.Peek().LoadDepthData(sr, er, ref data, dataOffset);

                    if (er == TotalHeight && seq > _newestSequence) {
                        _newestSequence = seq;
                        ff = _frameBuffer.Dequeue();
                        _frameBuffer.Peek().CopyFrom(ff);
                    }
                }
                if (ff != null) {
                    ff.cameraPos = FrameSource.cameraPosition;
                    ff.cameraRot = FrameSource.cameraRotation;
                    FrameSource.frameQueue.Enqueue(ff);
                }

            } catch (Exception e) {
                Debug.LogError(e);
            }
        }
    }

}