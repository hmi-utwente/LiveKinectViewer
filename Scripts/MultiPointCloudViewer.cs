using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HMIMR.DepthStreaming {

    public class MultiPointCloudViewer : MonoBehaviour {
        public Material m_material;
        public List<FrameSource> frameSources = new List<FrameSource>();
        private List<PointCloudViewer> pointCloudViewers = new List<PointCloudViewer>();
        private int lastViewerUpdate = 0;
        private int nextUpdate = 1;
        public int updateEvery = 2;

        // Use this for initialization
        void Start() {
            foreach (FrameSource frameSource in frameSources) {
                GameObject newObj = new GameObject(frameSource.name + "Viewer", typeof(PointCloudViewer));
                newObj.transform.SetParent(transform);
                PointCloudViewer newViewer = newObj.GetComponent<PointCloudViewer>();
                newViewer.frameSource = frameSource;
                newViewer.m_material = Instantiate(m_material);
                pointCloudViewers.Add(newViewer);
            }
        }

        // Update is called once per frame
        void Update() {
            nextUpdate--;
            if (nextUpdate <= 0) {
                nextUpdate = updateEvery;
                lastViewerUpdate++;
                if (lastViewerUpdate >= pointCloudViewers.Count)
                    lastViewerUpdate = 0;
                pointCloudViewers[lastViewerUpdate].UpdateNow();
            }
        }
    }

}