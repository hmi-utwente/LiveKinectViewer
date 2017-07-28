using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace HMIMR.DepthStreaming {

    public abstract class DepthStreamingProcessor {
        public readonly DepthDeviceType DeviceType;
        public readonly ushort TotalWidth;
        public readonly ushort TotalHeight;
        public readonly ushort MaxLinesPerBlock;
        public readonly string DeviceGUID;
        public readonly DepthCameraIntrinsics CameraIntrinsics;

        public readonly DepthStreamingSource FrameSource;

        protected DepthStreamingProcessor(DepthStreamingSource fs, DepthDeviceType t, DepthCameraIntrinsics cameraIntrinsics,
            ushort w, ushort h, ushort ml, string guid) {
            DeviceType = t;
            TotalWidth = w;
            TotalHeight = h;
            MaxLinesPerBlock = ml;
            DeviceGUID = guid;
            CameraIntrinsics = cameraIntrinsics;
            FrameSource = fs;
        }

        // How do we make explicit that a DepthStreamingProcessor is (now) responsible for calling:
        //  frameSource.frameQueue.enqueue(...)

        public abstract void HandleData(ushort startRow, ushort endRow,
            UInt32 sequence, ref byte[] data, int dataOffset);

        /*
        public abstract byte[] GetRawColorData();
        public abstract ushort[] GetRawDepthData();
        public abstract Color[] GetDepthData();
        */

        public abstract void Close();
    }

}