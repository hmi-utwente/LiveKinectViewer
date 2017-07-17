using UnityEngine;
using System.Collections;
using System.Threading;
using System;
using System.Collections.Generic;

public class KinectStreamingSource : FrameSource
{
    public int responsePort = 1339;
    private KinectStreamingListener listener;
    public float dropsPerSecond = 0.0f;

    private Vector3 cameraPos = new Vector3();
    private Quaternion cameraRot = new Quaternion();

    private int texWidth = KinectStreamingListener.LineWidth;
    private int texHeight = KinectStreamingListener.TextureHeight;

    Thread thread;
    private bool running = false;

    private int packagesCount = 0;
    float dt = 0F;
    private int frameCount = 0;
    private float lastPrint = 0;

    // Use this for initialization
    private new void Start()
    {
        base.Start();

        listener = new KinectStreamingListener(responsePort);

        thread = new Thread(Run);
        thread.Start();
    }

    void OnApplicationQuit()
    {
        listener.Close();
        running = false;
        thread.Join();  // block till thread is finished
    }

    void Update()
    {
        dt += Time.deltaTime;
        if (dt > 1.0F)
        {
            if(frameCount!=0)
                Debug.Log("Packages Per Frame: " + ((float)packagesCount / frameCount).ToString());
            frameCount = 0;
            packagesCount = 0;
            dt -= 1.0F;
        }
        if (Time.time > lastPrint + 1)

        dropsPerSecond = listener.dropsPerSecond;

        cameraPos = cameraTransform.position;
        cameraRot = cameraTransform.rotation;
        //listener.ColorLoadRaw (ref colorTex);
        //colorTex.Apply ();
        lock (listener.frameCountersLock)
        {
            List<uint> toremove = new List<uint>();

            foreach (KeyValuePair<uint, CounterFrame> entry in listener.frameCounters)
            {
                CounterFrame counter = entry.Value;
                //Debug.Log(counter.timeStamp);
                if (counter.timeStamp + 5 < (float)(DateTime.Now.ToUniversalTime() - new DateTime(2017, 7, 16)).TotalSeconds)  ///// todo
                {
                    packagesCount += counter.count;
                    frameCount++;
                    toremove.Add(entry.Key);
                }
            }
            foreach (uint key in toremove)
            {
                listener.frameCounters.Remove(key);
            }
        }
    }

    void Run()
    {
        running = true;
        while (running)
        {
            PreFrameObj newFrame = new PreFrameObj();
            newFrame.DXT1_colors = listener.GetRawColorData();
            newFrame.colSize = new Vector2(KinectStreamingListener.LineWidth, KinectStreamingListener.TextureHeight);
            newFrame.positions = listener.ComputeDepthColors();
            newFrame.posSize = new Vector2(KinectStreamingListener.LineWidth, KinectStreamingListener.TextureHeight);
            newFrame.cameraPos = cameraPos;
            newFrame.cameraRot = cameraRot;

            frameQueue.Enqueue(newFrame);
        }
    }

}

