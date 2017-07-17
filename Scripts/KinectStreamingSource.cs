using UnityEngine;
using System.Collections;
using System.Threading;

public class KinectStreamingSource : FrameSource {
	public int responsePort = 1339;
	private KinectStreamingListener listener;
	public float dropsPerSecond = 0.0f;

	private Vector3 cameraPos = new Vector3();
	private Quaternion cameraRot = new Quaternion();

	private int texWidth = KinectStreamingListener.LineWidth;
	private int texHeight = KinectStreamingListener.TextureHeight;

    Thread thread;
    private bool running = false;

    // Use this for initialization
    private new void Start()
    {
        base.Start();
        
		listener = new KinectStreamingListener (responsePort);

        thread = new Thread(Run);
        thread.Start();
    }

	void OnApplicationQuit() {
		listener.Close ();
        running = false;
        thread.Join();  // block till thread is finished
    }

	void Update (){
		dropsPerSecond = listener.dropsPerSecond;

        cameraPos = cameraTransform.position;
        cameraRot = cameraTransform.rotation;
        //listener.ColorLoadRaw (ref colorTex);
        //colorTex.Apply ();
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

