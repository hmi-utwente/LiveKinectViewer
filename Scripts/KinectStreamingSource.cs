using UnityEngine;
using System.Collections;

public class KinectStreamingSource : FrameSource {

	public string ip = "127.0.0.1";
	public int responsePort = 1339;
	private KinectStreamingListener listener;
	public float msgFps = 0.0f;

	private float fpsSecondsAverage = 2.0f;
	private float tStartSeconds = 0.0f;
	private int frameCounter;

	public GameObject cameraTransform;
	private Vector3 cameraPos = new Vector3();
	private Quaternion cameraRot = new Quaternion();


	public Texture2D colorTex;
	public MeshRenderer debugColor;

	private int texWidth = KinectStreamingListener.LineWidth;
	private int texHeight = KinectStreamingListener.TextureHeight;

	// Use this for initialization
	void Start () {
		colorTex = new Texture2D (KinectStreamingListener.LineWidth, KinectStreamingListener.TextureHeight, TextureFormat.DXT1, false);
		colorTex.wrapMode = TextureWrapMode.Clamp;
		debugColor.material.mainTexture = colorTex;
		listener = new KinectStreamingListener (responsePort);
	}

	void OnApplicationQuit() {
		listener.Close ();
	}

	void Update () {
		cameraPos = cameraTransform.transform.position;
		cameraRot = cameraTransform.transform.rotation;

		listener.ColorLoadRaw (ref colorTex);
		colorTex.Apply ();

		Color[] _positions = listener.ComputeDepthColors();
		Color[] _colors = colorTex.GetPixels();

		PreFrameObj newFrame = new PreFrameObj();
		newFrame.colors = _colors;
		newFrame.colSize = new Vector2(KinectStreamingListener.LineWidth, KinectStreamingListener.TextureHeight);
		newFrame.positions = _positions;
		newFrame.posSize = new Vector2(KinectStreamingListener.LineWidth, KinectStreamingListener.TextureHeight);
		newFrame.cameraPos = cameraPos;
		newFrame.cameraRot = cameraRot;

		frameQueue.Enqueue (newFrame);

		if (Time.time >= tStartSeconds + fpsSecondsAverage) {
			msgFps = frameCounter / fpsSecondsAverage;
			tStartSeconds = Time.time;
			frameCounter = 0;
		}
	}

}

