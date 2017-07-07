using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class KinectViewer : MonoBehaviour
{
    private KinectUpdateThread kinectUpdateThread;
    public Material m_material;

    private Mesh mesh;
    public int m_maxPointsInInstance = 10000;
    public int m_maxPoints = 217088;

    // Use this for initialization
    void Start()
    {
        kinectUpdateThread = GetComponent<KinectUpdateThread>();

        CreateMesh();
    }

    // Update is called once per frame
    void Update()
    {
        Texture2D newColTex = kinectUpdateThread.GetColTex();
        Texture2D newPosTex = kinectUpdateThread.GetPosTex();

        if (newColTex != null)
        {
            Destroy(m_material.GetTexture("_ColorTex"));
            m_material.SetTexture("_ColorTex", newColTex);
        }

        if (newPosTex != null)
        {
            Destroy(m_material.GetTexture("_PositionTex"));
            m_material.SetTexture("_PositionTex", newPosTex);
        }

    }


    void CreateMesh()
    {
        int totIndex = 0;
        int meshId = 0;

        while (totIndex < m_maxPoints)
        {
            GameObject go = new GameObject();
            go.name = "Mesh" + meshId++;
            MeshFilter mf = go.AddComponent<MeshFilter>();
            Mesh msh = new Mesh();
            mf.sharedMesh = msh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = m_material;
            go.transform.parent = transform;

            int pointsInMesh = Math.Min(m_maxPointsInInstance, m_maxPoints- totIndex);
            Vector3[] verts = new Vector3[pointsInMesh];
            int[] faces = new int[pointsInMesh * 3];
            Vector2[] uvs0 = new Vector2[pointsInMesh];  // pos
            Vector2[] uvs1 = new Vector2[pointsInMesh];  // col

            for (int i = 0; i < pointsInMesh; ++i)
            {
                verts[i] = new Vector3(0f, 0f, 0f);
                faces[i * 3] = i; // make sure every vertex is at least once in the faces/triangle array, or it won't get rendered

                float xx = totIndex % kinectUpdateThread.depthWidth;
                float yy = totIndex / (float)kinectUpdateThread.depthWidth;

                xx /= kinectUpdateThread.depthWidth;
                yy /= kinectUpdateThread.depthHeight;

                uvs0[i] = new Vector2(xx, yy);
                uvs1[i] = new Vector2(xx, yy);

                totIndex++;
            }

            msh.vertices = verts;
            msh.triangles = faces;
            msh.uv = uvs0;
            msh.uv2 = uvs1;
            msh.bounds = new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f));
        }
    }
}
