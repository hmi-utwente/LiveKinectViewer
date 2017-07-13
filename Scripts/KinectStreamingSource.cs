using UnityEngine;
using System.Collections;

public class KinectStreamingSource : FrameSource {

	public string ip = "127.0.0.1";
	public int responsePort = 1339;
	private KinectStreamingListener listener;
	public float dropsPerSecond = 0.0f;

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

	void Update (){
		dropsPerSecond = listener.dropsPerSecond;

		listener.ColorLoadRaw (ref colorTex);
		colorTex.Apply ();

		PreFrameObj newFrame = new PreFrameObj();
		newFrame.colors = colorTex.GetPixels();
		newFrame.colSize = new Vector2(KinectStreamingListener.LineWidth, KinectStreamingListener.TextureHeight);
		newFrame.positions = listener.ComputeDepthColors();
		newFrame.posSize = new Vector2(KinectStreamingListener.LineWidth, KinectStreamingListener.TextureHeight);
		newFrame.cameraPos = cameraTransform.transform.position;
		newFrame.cameraRot = cameraTransform.transform.rotation;

		frameQueue.Enqueue (newFrame);
	}

}

