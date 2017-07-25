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

        
		protected DepthStreamingProcessor(DepthDeviceType t, DepthCameraIntrinsics cameraIntrinsics,
			ushort w, ushort h, ushort ml, string guid) {
			DeviceType = t;
			TotalWidth = w;
			TotalHeight = h;
			MaxLinesPerBlock = ml;
			DeviceGUID = guid;
			CameraIntrinsics = cameraIntrinsics;
		}

		public abstract void HandleData(ushort startRow, ushort endRow,
			UInt32 sequence, ref byte[] data, int dataOffset);
        
        
		public abstract byte[] GetRawColorData();
		public abstract ushort[] GetRawDepthData();
		public abstract Color[] GetDepthData();
            
		public void Close() {}
	}

}
