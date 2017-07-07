using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using Windows.Kinect;

public class KinectUpdateThread : MonoBehaviour {

    public MultiSourceManager _MultiManager;

    private KinectSensor _Sensor;
    private CoordinateMapper _Mapper;

    Thread thread;
    private bool running = false;

    private Color[] positions;
    private System.Object posLock = new System.Object();
    private bool newPos = false;
    private Color[] colors;
    private System.Object colLock = new System.Object();
    private bool newCol = false;

    public int colorWidth;
    public int colorHeight;
    public int depthWidth;
    public int depthHeight;

    // Use this for initialization
    void Start () {

        _Sensor = KinectSensor.GetDefault();
        if (_Sensor != null)
        {
            _Mapper = _Sensor.CoordinateMapper;
            
            var colorFrameDesc = _Sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);
            colorWidth = colorFrameDesc.Width;
            colorHeight = colorFrameDesc.Height;

            var frameDesc = _Sensor.DepthFrameSource.FrameDescription;
            depthWidth = frameDesc.Width;
            depthHeight = frameDesc.Height;

            Debug.Log("width: " + depthWidth + " height: " + depthHeight);

            if (!_Sensor.IsOpen)
            {
                _Sensor.Open();
            }
        }


        thread = new Thread(Run);
        thread.Start();
	}


    void Run()
    {
        running = true;
        while (running)
        {
            byte[] colorData = _MultiManager.GetColorData();
            ushort[] depthData = _MultiManager.GetDepthData();

            ColorSpacePoint[] colorSpace = new ColorSpacePoint[depthData.Length];
            _Mapper.MapDepthFrameToColorSpace(depthData, colorSpace);


            Color[] _positions = new Color[depthWidth* depthHeight];
            Color[] _colors = new Color[depthWidth * depthHeight];

            for (int y = 0; y < depthHeight; y++)
            {
                for (int x = 0; x < depthWidth; x++)
                {
                    int fullIndex = (y * depthWidth) + x;

                    float zc = 100 * depthData[fullIndex] / 65535F; 
                    float xc = 1 - (x / (float)depthWidth) - 0.5F;
                    float yc = 1 - (y / (float)depthHeight) - 0.5F;

                    xc *= zc * (depthWidth / (float)depthHeight);
                    yc *= zc;

                    _positions[fullIndex] = new Color(xc, yc, zc);

                    int colorIndex = (((int)colorSpace[fullIndex].Y * colorWidth) + (int)colorSpace[fullIndex].X) * 4;
                    if (colorIndex >= 0 && colorIndex < colorData.Length)
                        _colors[fullIndex] = new Color(colorData[colorIndex] / 255F, colorData[colorIndex + 1] / 255F, colorData[colorIndex + 2] / 255F);
                    else
                        _colors[fullIndex] = Color.black;
                }
            }




            lock (posLock)
            {
                positions = _positions;
                newPos = true;
            }

            lock (colLock)
            {
                colors = _colors;
                newCol = true;
            }
        }
    }

    private void OnApplicationQuit()
    {
        running = false;
        thread.Join();  // block till thread is finished
    }

    public Texture2D GetPosTex()
    {
        if (!newPos) return null;

        Texture2D posTex = new Texture2D(depthWidth, depthHeight, TextureFormat.RGBAFloat, false);
        posTex.wrapMode = TextureWrapMode.Repeat;
        posTex.filterMode = FilterMode.Point;

        lock (posLock)
        {
            posTex.SetPixels(positions);
            newPos = false;
        }

        posTex.Apply();
        return posTex;
    }

    public Texture2D GetColTex()
    {
        if (!newCol) return null;

        Texture2D colTex = new Texture2D(depthWidth, depthHeight, TextureFormat.RGBAFloat, false);
        colTex.wrapMode = TextureWrapMode.Repeat;
        colTex.filterMode = FilterMode.Point;

        lock (colLock)
        {
            colTex.SetPixels(colors);
            newCol = false;
        }

        colTex.Apply();
        return colTex;
    }

    // Update is called once per frame
    void Update () {
		
	}
}
