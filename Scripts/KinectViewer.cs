using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class KinectViewer : MonoBehaviour {
    private KinectUpdateThread kinectUpdateThread;
    public Material m_material;

    private Mesh mesh;
    public int m_maxPointsInInstance = 10000;
    public int m_maxPoints = 217088;

    // Use this for initialization
    void Start () {
        kinectUpdateThread = GetComponent<KinectUpdateThread>();

        CreateMesh();
    }
	
	// Update is called once per frame
	void Update () {
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
        int ic = 0;
        Mesh msh = null;
        Vector3[] verts = new Vector3[m_maxPointsInInstance * 4];
        int[] faces = new int[m_maxPointsInInstance * 6];
        Vector2[] uvs0 = new Vector2[m_maxPointsInInstance * 4];  // quad
        Vector2[] uvs1 = new Vector2[m_maxPointsInInstance * 4];  // pos
        Vector2[] uvs2 = new Vector2[m_maxPointsInInstance * 4];  // col


        for (int i = 0; i < m_maxPoints; ++i)
        {
            verts[ic * 4 + 0] = new Vector3(0f, 0f, 0f);
            verts[ic * 4 + 1] = new Vector3(0f, 0f, 0f);
            verts[ic * 4 + 2] = new Vector3(0f, 0f, 0f);
            verts[ic * 4 + 3] = new Vector3(0f, 0f, 0f);

            faces[ic * 6 + 0] = ic * 4 + 0;
            faces[ic * 6 + 1] = ic * 4 + 2;
            faces[ic * 6 + 2] = ic * 4 + 1;

            faces[ic * 6 + 3] = ic * 4 + 1;
            faces[ic * 6 + 4] = ic * 4 + 2;
            faces[ic * 6 + 5] = ic * 4 + 3;

            float xx = (i % kinectUpdateThread.depthWidth) / Convert.ToSingle(kinectUpdateThread.depthWidth);
            float yy = Mathf.Floor((i / kinectUpdateThread.depthWidth)) / Convert.ToSingle(kinectUpdateThread.depthHeight);

            uvs0[ic * 4 + 0] = new Vector2(1f, -1f);
            uvs0[ic * 4 + 1] = new Vector2(1f, 1f);
            uvs0[ic * 4 + 2] = new Vector2(-1f, -1f);
            uvs0[ic * 4 + 3] = new Vector2(-1f, 1f);

            uvs1[ic * 4 + 0] = new Vector2(xx, yy);
            uvs1[ic * 4 + 1] = new Vector2(xx, yy);
            uvs1[ic * 4 + 2] = new Vector2(xx, yy);
            uvs1[ic * 4 + 3] = new Vector2(xx, yy);

            uvs2[ic * 4 + 0] = new Vector2(xx, yy);
            uvs2[ic * 4 + 1] = new Vector2(xx, yy);
            uvs2[ic * 4 + 2] = new Vector2(xx, yy);
            uvs2[ic * 4 + 3] = new Vector2(xx, yy);

            if (ic < m_maxPointsInInstance)
            {
                if (ic == 0)
                {
                    GameObject go = new GameObject();
                    MeshFilter mf = go.AddComponent<MeshFilter>();
                    msh = new Mesh();
                    mf.sharedMesh = msh;
                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.material = m_material;
                    go.transform.parent = transform;
                }
            }
            ++ic;
            if (ic >= m_maxPointsInInstance)
            {
                ic = 0;
                msh.vertices = verts;
                msh.triangles = faces;
                msh.uv = uvs0;
                msh.uv2 = uvs1;
                msh.uv3 = uvs2;
                msh.bounds = new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f));
            }
        }
    }
}
