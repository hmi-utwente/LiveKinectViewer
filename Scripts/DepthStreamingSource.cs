using UnityEngine;
using System.Collections;
using System.Threading;
using System;
using System.Collections.Generic;

namespace HMIMR.DepthStreaming {

    public class DepthStreamingSource : FrameSource {
        public int dataPort = 1339;
        private DepthStreamingListener listener;

        private new void Start() {
            base.Start();
            listener = new DepthStreamingListener(dataPort, this);
        }

        void OnApplicationQuit() {
            listener.Close();
        }

        void Update() { }

    }
}
