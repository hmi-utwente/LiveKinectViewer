using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System;
using System.Linq;

namespace HMIMR.DepthStreaming {

    public class SequencedFrame : FastFrame {
        public BitArray ReceivedFrames;

        private readonly DepthStreamingProcessor _processor;

        public SequencedFrame(DepthStreamingProcessor p) : base(p) {
            _processor = p;
            Reset();
        }

        public override void Release() {
            ((VSyncProcessor) _processor).ReturnFromRender(this);
        }

        public void Reset() {
            ReceivedFrames = new BitArray(_processor.TotalHeight, false);
        }

        public bool IsComplete() {
            for (var i = 0; i < ReceivedFrames.Count; i++) {
                if (!ReceivedFrames.Get(i)) return false;
            }
            return true;
        }

        public int CountMissing() {
            int res = 0;
            for (var i = 0; i < ReceivedFrames.Count; i++) {
                if (!ReceivedFrames.Get(i)) res++;
            }
            return res;
        }

        public void MarkAsLoaded(ushort sr, ushort er) {
            for (ushort l = sr; l < er; l++) {
                ReceivedFrames.Set(l, true);
            }
        }
    }

    public class VSyncProcessor : DepthStreamingProcessor {

        private readonly int _frameBufferSize = 16;
        private readonly Queue<SequencedFrame> _unusedQueue;
        private readonly Dictionary<UInt32, SequencedFrame> _frameBuffer;

        private readonly object _unusedQueueLock = new object();
        private readonly object _frameBufferLock = new object();
        private readonly Thread _processThread;

        private bool _processing;
        private UInt32 _lastSequenceRendered = 0;

        public VSyncProcessor(FrameSource fs, DepthDeviceType t, DepthCameraIntrinsics cI,
            ushort w, ushort h, ushort ml, string guid)
            : base(fs, t, cI, w, h, ml, guid) {
            _frameBuffer = new Dictionary<UInt32, SequencedFrame>();
            _unusedQueue = new Queue<SequencedFrame>();
            for (int i = 0; i < _frameBufferSize; i++) {
                _unusedQueue.Enqueue(new SequencedFrame(this));
            }

            _processThread = new Thread(new ThreadStart(Process));
            _processThread.Start();
        }

        public void ReturnFromRender(SequencedFrame s) {
            lock (_unusedQueueLock) {
                _unusedQueue.Enqueue(s);
            }
        }

        public override void Close() {
            _processing = false;
            if (_processThread != null)
                _processThread.Join(1000);
        }

        private void Process() {
            _processing = true;
            try {
                while (_processing) {
                    lock (_frameBufferLock) {
                        UInt32 remove = 0;
                        foreach (KeyValuePair<UInt32, SequencedFrame> sequencedFrame in _frameBuffer) {
                            if (sequencedFrame.Key < _lastSequenceRendered) {
                                remove = sequencedFrame.Key;
                                //Debug.Log("A newer frame has already been rendered: "+remove);
                                break;
                            }

                            if (sequencedFrame.Value.IsComplete()) {
                                sequencedFrame.Value.cameraPos = FrameSource.cameraPosition;
                                sequencedFrame.Value.cameraRot = FrameSource.cameraRotation;
                                _lastSequenceRendered = sequencedFrame.Key;
                                remove = sequencedFrame.Key;
                                break;
                            }
                        }

                        if (remove > 0) {
                            SequencedFrame removeFrame = _frameBuffer[remove];
                            _frameBuffer.Remove(remove);
                            if (remove == _lastSequenceRendered) {
                                FrameSource.frameQueue.Enqueue(removeFrame);
                            } else {
                                lock (_unusedQueueLock) {
                                    _unusedQueue.Enqueue(removeFrame);
                                }
                            }
                        }
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
            if (seq < _lastSequenceRendered) return;

            lock (_frameBufferLock)
            lock (_unusedQueueLock) {
                if (_frameBuffer.ContainsKey(seq)) {
                    _frameBuffer[seq].LoadData(sr, er, ref data, dataOffset);
                    _frameBuffer[seq].MarkAsLoaded(sr, er);
                    //Debug.Log("Using old frame: "+seq);
                } else if (_unusedQueue.Count > 0) {
                    _frameBuffer[seq] = _unusedQueue.Dequeue();
                    _frameBuffer[seq].Reset();
                    _frameBuffer[seq].LoadData(sr, er, ref data, dataOffset);
                    _frameBuffer[seq].MarkAsLoaded(sr, er);
                    //Debug.Log("Dequeued for: "+seq);
                } else if (_frameBuffer.Count > 0) {
                    UInt32 oldest = _frameBuffer.Keys.Min();
                    SequencedFrame old = _frameBuffer[oldest];
                    _frameBuffer.Remove(oldest);
                    Debug.LogWarning("Dropping frame with seq: " + oldest + ", missing: " +
                                     old.CountMissing() + " of " + TotalHeight);
                    old.Reset();
                    _frameBuffer[seq] = old;
                    _frameBuffer[seq].LoadData(sr, er, ref data, dataOffset);
                    _frameBuffer[seq].MarkAsLoaded(sr, er);
                } else {
                    Debug.LogWarning("Not enough (unused) framebuffers.");
                }
            }
        }
    }

}