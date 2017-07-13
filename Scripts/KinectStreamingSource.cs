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


	public Texture2D debugColorTex;
	//public Texture2D debugDepthTex;

	public MeshRenderer debugColor;
	public MeshRenderer debugDepth;

	private ushort[] _DepthData;

	private int texWidth = KinectStreamingListener.LineWidth;
	private int texHeight = KinectStreamingListener.TextureHeight;


	private float startupDelay = 2.0f;

	// Use this for initialization
	void Start () {
		debugColorTex = new Texture2D (512, 424);
		debugColorTex.wrapMode = TextureWrapMode.Clamp;
		debugColor.material.mainTexture = debugColorTex;
		//debugDepthTex = new Texture2D (512, 424);
		//debugDepthTex.wrapMode = TextureWrapMode.Clamp;
		//debugDepth.material.mainTexture = debugDepthTex;
		listener = new KinectStreamingListener (responsePort);


		_DepthData = new ushort[texWidth * texHeight];
			
		Debug.Log("Created Listener.");
	}

	void OnApplicationQuit() {
		Debug.Log ("Closing Listen Socket");
		listener.Close ();
	}
	
	// Update is called once per frame
	void Update () {

		cameraPos = cameraTransform.transform.position;
		cameraRot = cameraTransform.transform.rotation;


		while (listener.QueuedMessages () > 0) {
			KinectFrame frame = listener.Read();
			frameCounter++;

			if (frame.lines == 0)
				continue;

			Texture2D rawTex = new Texture2D (KinectStreamingListener.LineWidth, frame.lines, TextureFormat.DXT1, false);
			rawTex.LoadRawTextureData (frame.colorData);
			//debugColorTex.SetPixels(0, frame.startRow, 512, frame.lines, frame.colorDataC);
			debugColorTex.SetPixels(0, frame.startRow, KinectStreamingListener.LineWidth, frame.lines, rawTex.GetPixels());
			//debugDepthTex.SetPixels(0, frame.startRow, 512, frame.lines, frame.depthDataC);
			System.Buffer.BlockCopy(frame.depthData16, 0, _DepthData, frame.startRow*2*texWidth, frame.lines*2*texWidth);
			listener.Release (frame);
		}

		debugColorTex.Apply ();

		if (Time.time < startupDelay)
			return;
		
		//debugDepthTex.Apply ();

		Color[] _positions = new Color[texHeight*texWidth];

		for (int y = 0; y < texHeight; y++) {
			for (int x = 0; x < texWidth; x++) {
				int fullIndex = (y * texWidth) + x;

				float zc = 71 * _DepthData[fullIndex] / 65535F;
				float xc = 1 - (x / (float)texWidth) - 0.5F;
				float yc = 1 - (y / (float)texHeight) - 0.5F;

				xc *= zc * (texWidth / (float)texHeight);
				yc *= zc;

				_positions[fullIndex] = new Color(xc, yc, zc);
			}
		}



		Color[] _colors = debugColorTex.GetPixels();

		PreFrameObj newFrame = new PreFrameObj();
		newFrame.colors = _colors;
		newFrame.colSize = new Vector2(texWidth, texHeight);
		newFrame.positions = _positions;
		newFrame.posSize = new Vector2(texWidth, texHeight);
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

