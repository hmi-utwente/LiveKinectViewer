using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Windows.Kinect;
using System.Threading;

public class KinectSource : FrameSource
{
    private int colorWidth;
    private int colorHeight;
    private int depthWidth;
    private int depthHeight;

    public GameObject cameraTransform;
    private Vector3 cameraPos = new Vector3();
    private Quaternion cameraRot = new Quaternion();

    private KinectSensor _Sensor;
    private MultiSourceFrameReader _Reader;
    private CoordinateMapper _Mapper;
    private ushort[] _DepthData;
    private byte[] _ColorData;

    Thread thread;
    private bool running = false;

    // Use this for initialization
    void Start()
    {
        _Sensor = KinectSensor.GetDefault();

        if (_Sensor != null)
        {
            _Mapper = _Sensor.CoordinateMapper;
            _Reader = _Sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth);

            var colorFrameDesc = _Sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);
            colorWidth = colorFrameDesc.Width;
            colorHeight = colorFrameDesc.Height;
            _ColorData = new byte[colorFrameDesc.BytesPerPixel * colorFrameDesc.LengthInPixels];

            var depthFrameDesc = _Sensor.DepthFrameSource.FrameDescription;
            depthWidth = depthFrameDesc.Width;
            depthHeight = depthFrameDesc.Height;
            _DepthData = new ushort[depthFrameDesc.LengthInPixels];

            if (!_Sensor.IsOpen)
            {
                _Sensor.Open();
            }
        }

        thread = new Thread(Run);
        thread.Start();
    }

    private void Update()
    {
        cameraPos = cameraTransform.transform.position;
        cameraRot = cameraTransform.transform.rotation;
    }

    void Run()
    {
        running = true;
        while (running)
        {
            if (_Reader != null)
            {
                var frame = _Reader.AcquireLatestFrame();
                if (frame != null)
                {
                    var colorFrame = frame.ColorFrameReference.AcquireFrame();
                    if (colorFrame != null)
                    {
                        var depthFrame = frame.DepthFrameReference.AcquireFrame();
                        if (depthFrame != null)
                        {
                            colorFrame.CopyConvertedFrameDataToArray(_ColorData, ColorImageFormat.Rgba);
                            depthFrame.CopyFrameDataToArray(_DepthData);

                            ColorSpacePoint[] colorSpace = new ColorSpacePoint[_DepthData.Length];
                            _Mapper.MapDepthFrameToColorSpace(_DepthData, colorSpace);

                            Color[] _positions = new Color[depthWidth * depthHeight];
                            Color[] _colors = new Color[depthWidth * depthHeight];

                            for (int y = 0; y < depthHeight; y++)
                            {
                                for (int x = 0; x < depthWidth; x++)
                                {
                                    int fullIndex = (y * depthWidth) + x;

                                    float zc = 71 * _DepthData[fullIndex] / 65535F;
                                    float xc = 1 - (x / (float)depthWidth) - 0.5F;
                                    float yc = 1 - (y / (float)depthHeight) - 0.5F;

                                    xc *= zc * (depthWidth / (float)depthHeight);
                                    yc *= zc;

                                    _positions[fullIndex] = new Color(xc, yc, zc);

                                    int colorIndex = (((int)colorSpace[fullIndex].Y * colorWidth) + (int)colorSpace[fullIndex].X) * 4;
                                    if (colorIndex >= 0 && colorIndex < _ColorData.Length)
                                        _colors[fullIndex] = new Color(_ColorData[colorIndex] / 255F, _ColorData[colorIndex + 1] / 255F, _ColorData[colorIndex + 2] / 255F);
                                    else
                                        _colors[fullIndex] = Color.black;
                                }
                            }

                            PreFrameObj newFrame = new PreFrameObj();
                            newFrame.colors = _colors;
                            newFrame.colSize = new Vector2(depthWidth, depthHeight);
                            newFrame.positions = _positions;
                            newFrame.posSize = new Vector2(depthWidth, depthHeight);
                            newFrame.cameraPos = cameraPos;
                            newFrame.cameraRot = cameraRot;


                            frameQueue.Enqueue(newFrame);

                            depthFrame.Dispose();
                            depthFrame = null;
                        }

                        colorFrame.Dispose();
                        colorFrame = null;
                    }

                    frame = null;
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        thread.Join();  // block till thread is finished
        if (_Reader != null)
        {
            _Reader.Dispose();
            _Reader = null;
        }

        if (_Sensor != null)
        {
            if (_Sensor.IsOpen)
            {
                _Sensor.Close();
            }

            _Sensor = null;
        }
    }

}
