using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class RandomSource : FrameSource
{
    private int frameWidth = 512;
    private int frameHeight = 424;


    Thread thread;
    private bool running = false;

    // Use this for initialization
    void Start()
    {
        thread = new Thread(Run);
        thread.Start();
    }

    void Run()
    {
        System.Random random = new System.Random();
        running = true;
        while (running)
        {
            Color[] _positions = new Color[frameWidth * frameHeight];
            Color[] _colors = new Color[frameWidth * frameHeight];

            for (int y = 0; y < frameHeight; y++)
            {
                for (int x = 0; x < frameWidth; x++)
                {
                    int fullIndex = (y * frameWidth) + x;

                    _positions[fullIndex] = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());

                    _colors[fullIndex] = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()); ;
                }
            }

            PreFrameObj newFrame = new PreFrameObj();
            newFrame.colors = _colors;
            newFrame.colSize = new Vector2(frameWidth, frameHeight);
            newFrame.positions = _positions;
            newFrame.posSize = new Vector2(frameWidth, frameHeight);
            newFrame.cameraPos = new Vector3(0, 0, 0);
            newFrame.cameraRot = new Quaternion(0, 0, 0, 0);

            frameQueue.Enqueue(newFrame);
        }
    }


    void OnApplicationQuit()
    {
        running = false;
        thread.Join();  // block till thread is finished
    }
}
