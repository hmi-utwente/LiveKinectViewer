using UnityEngine;
using System.Collections;
using System.Threading;
using System;
using System.Collections.Generic;

namespace HMIMR.DepthStreaming {
    public class DepthStreamingSource : FrameSource {
        public int dataPort = 1339;
        private DepthStreamingListener listener;
        public float dropsPerSecond = 0.0f;

        private Vector3 cameraPos;
        private Quaternion cameraRot;

        Thread thread;
        private bool running = false;


        // Use this for initialization
        private new void Start() {
            base.Start();

            listener = new DepthStreamingListener(dataPort);

            thread = new Thread(Run);
            thread.Start();
        }

        void OnApplicationQuit() {
            listener.Close();
            running = false;
            if (thread != null)
                thread.Join(500);
        }

        void Update() {
            cameraPos = cameraTransform.position;
            cameraRot = cameraTransform.rotation;
        }

        void Run() {
            running = true;
            while (running) {
                //Thread.Sleep((int) (1000.0f/240.0f));
                if (listener.processor == null) continue;
                
                PreFrameObj newFrame = new PreFrameObj();
                newFrame.DXT1_colors = listener.processor.GetRawColorData();
                newFrame.colSize = new Vector2(listener.processor.TotalWidth, listener.processor.TotalHeight);
                newFrame.positions = listener.processor.GetDepthData();
                newFrame.posSize = new Vector2(listener.processor.TotalWidth, listener.processor.TotalHeight);
                newFrame.cameraPos = cameraPos;
                newFrame.cameraRot = cameraRot;

                frameQueue.Enqueue(newFrame);
            }
        }

    }
}
