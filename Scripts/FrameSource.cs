using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;

namespace HMIMR.DepthStreaming {
    public class FrameSource : MonoBehaviour {

        public Transform cameraTransform;

        [HideInInspector]
        public LockingQueue frameQueue = new LockingQueue();

        // Use this for initialization
        public void Start() {
            if (cameraTransform == null) {
                cameraTransform = transform;
            }
        }

        // Update is called once per frame
        void Update() { }


        public FrameObj GetNewFrame() {
            APreFrameObj preObj = frameQueue.Poll();
            if (preObj != null) {
                FrameObj newFrame = new FrameObj();
                newFrame.posTex = new Texture2D((int)preObj.posSize.x, (int)preObj.posSize.y, TextureFormat.RGBAFloat,
                    false);
                newFrame.posTex.wrapMode = TextureWrapMode.Repeat;
                newFrame.posTex.filterMode = FilterMode.Point;
                newFrame.posTex.SetPixels(preObj.positions);
                newFrame.posTex.Apply();

                if (preObj.colors != null) {
                    newFrame.colTex = new Texture2D((int)preObj.colSize.x, (int)preObj.colSize.y,
                        TextureFormat.RGBAFloat, false);
                    newFrame.colTex.wrapMode = TextureWrapMode.Repeat;
                    newFrame.colTex.filterMode = FilterMode.Point;
                    newFrame.colTex.SetPixels(preObj.colors);
                    newFrame.colTex.Apply();
                } else if (preObj.DXT1_colors != null) {
                    newFrame.colTex = new Texture2D((int)preObj.colSize.x, (int)preObj.colSize.y, TextureFormat.DXT1,
                        false);
                    newFrame.colTex.wrapMode = TextureWrapMode.Clamp;
                    newFrame.colTex.filterMode = FilterMode.Point;
                    newFrame.colTex.LoadRawTextureData(preObj.DXT1_colors);
                    newFrame.colTex.Apply();
                } else if (preObj.JPEG_colors != null) {
                    newFrame.colTex = new Texture2D((int)preObj.colSize.x, (int)preObj.colSize.y);
                    newFrame.colTex.wrapMode = TextureWrapMode.Clamp;
                    newFrame.colTex.filterMode = FilterMode.Point;
                    newFrame.colTex.LoadImage(preObj.JPEG_colors);
                    newFrame.colTex.Apply();
                }

                newFrame.cameraPos = preObj.cameraPos;
                newFrame.cameraRot = preObj.cameraRot;

                newFrame.timeStamp = preObj.timeStamp;
                preObj.Release();
                return newFrame;
            } else
                return null;
        }

    }


    public class FrameObj {
        public Texture2D posTex;
        public Texture2D colTex;
        public Vector3 cameraPos;
        public Quaternion cameraRot;
        public float timeStamp;
    }

    public abstract class APreFrameObj {
        public Color[] positions;
        public Vector2 posSize;
        public Color[] colors;
        public byte[] DXT1_colors;
        public byte[] JPEG_colors;
        public Vector2 colSize;
        public Vector3 cameraPos;
        public Quaternion cameraRot;
        public float timeStamp;

        public abstract void Release();
    }

    public class PreFrameObj : APreFrameObj {
        public override void Release() { }
    }


    public class LockingQueue {
        private Queue<APreFrameObj> _queue = new Queue<APreFrameObj>();

        public APreFrameObj Dequeue() {
            //lock (_queue) {
                return _queue.Dequeue();
            //}
        }

        public APreFrameObj Poll() {
            //lock (_queue) {
                APreFrameObj returnObj = null;
                if (_queue.Count > 1) {
                    Debug.Log("Skipping " + (_queue.Count - 1) + " Frames");
                } else if (_queue.Count == 0) {
                    return null;
                }

                while (_queue.Count > 1) {
                    returnObj = _queue.Dequeue();
                    returnObj.Release();
                }

                return _queue.Dequeue();
            //}
        }

        public void Enqueue(APreFrameObj data) {
            if (data == null) throw new ArgumentNullException("data");

            //lock (_queue) {
                _queue.Enqueue(data);
            //}
        }
    }
}