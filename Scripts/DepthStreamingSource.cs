using UnityEngine;
using System.Collections;
using System.Threading;
using System;
using System.Collections.Generic;

namespace HMIMR.DepthStreaming {

    [RequireComponent(typeof(IMPRESS_UDPClient))]
    public class DepthStreamingSource : FrameSource {
        private DepthStreamingListener listener;
        private IMPRESS_UDPClient udpClient;

        [HideInInspector]
        public Vector3 cameraPosition;

        [HideInInspector]
        public Quaternion cameraRotation;

        private new void Start() {
            base.Start();
            udpClient = GetComponent<IMPRESS_UDPClient>();
            listener = new DepthStreamingListener(udpClient,this);
        }

        void OnApplicationQuit() {
            listener.Close();
        }

        void Update() {
            cameraPosition = cameraTransform.position;
            cameraRotation = cameraTransform.rotation;
        }

    }

}