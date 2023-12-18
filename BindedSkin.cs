using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

namespace SamsBackpack.BindedSkin
{
    public class BindedSkin : MonoBehaviour
    {
        [HideInInspector, SerializeField]
        public SkinnedMeshRenderer skin;

        //Runtime data
        [HideInInspector]
        public bool hasRuntimeData;
        private IBindedSkinAnchor[] anchors;
        public int anchorCount => anchors == null ? 0 : anchors.Length;
        private NativeArray<BindingData> bindedPoint;
        private NativeArray<float3> transformedPoints;
        private NativeArray<Matrix4x4> boneTransforms;
        private NativeArray<Matrix4x4> boneOrigins;
        private int maxPointPerAnchor;

        private void Start()
        {
            BuildRuntimeBinding();
        }

        private void OnDestroy()
        {
            if (hasRuntimeData)
                ReleaseRuntimeBinding();
        }

        private void Update()
        {
            if (hasRuntimeData)
                ApplyBinding();
        }

        /// <summary>
        /// Updates anchors in runtime. This operation can be quite heavy.
        /// </summary>
        public void RefreshRuntimeBinding()
        {
            ReleaseRuntimeBinding();
            BuildRuntimeBinding();
        }

        /// <summary>
        /// Loads all IBindedSkinAnchors that are children of this object into data optimized for runtime processing.
        /// </summary>
        public void BuildRuntimeBinding()
        {
            if (skin == null || skin.sharedMesh == null)
                return;

            //Load anchors
            anchors = GetComponentsInChildren<IBindedSkinAnchor>();
            if (anchors.Length == 0)
                return;

            //Reset to bind pose (Required for anchors to connect to the right vertices)
            GoToBindPose();

            //Converts all anchors NativeArray<BindingData> for fast processing.
            BoneWeight[] meshWeights = skin.sharedMesh.boneWeights;
            List<BindingData> tempList = new List<BindingData>();
            int anchorStartKey = 0;
            Matrix4x4 l2w = skin.transform.localToWorldMatrix;

            foreach (var anchor in anchors)
            {
                BindingPoint[] points = anchor.GetBindedVerticesIDs(skin.sharedMesh, l2w);
                maxPointPerAnchor = Mathf.Max(points.Length, maxPointPerAnchor);
                int pointCount = points.Length;

                for (int i = 0; i < pointCount; i++)
                {
                    tempList.Add(new BindingData(points[i].position, meshWeights[points[i].skinPointreference], anchorStartKey, i));
                }
                anchor.PointsKey = (anchorStartKey, pointCount);
                anchorStartKey += pointCount;
            }
            bindedPoint = new NativeArray<BindingData>(tempList.ToArray(), Allocator.Persistent);
            transformedPoints = new NativeArray<float3>(tempList.Count, Allocator.Persistent);

            //Build bone arrays
            boneTransforms = new NativeArray<Matrix4x4>(skin.bones.Length, Allocator.Persistent);
            boneOrigins = new NativeArray<Matrix4x4>(skin.sharedMesh.bindposes, Allocator.Persistent);

            hasRuntimeData = true;
        }

        /// <summary>
        /// Disposes of all data optimized for runtime processing.
        /// </summary>
        public void ReleaseRuntimeBinding()
        {
            hasRuntimeData = false;

            bindedPoint.Dispose();
        }

        /// <summary>
        /// Applies skinning to runtime-optimized data.
        /// </summary>
        private void ApplyBinding()
        {
            //Update bones matrices
            for (int i = 0; i < skin.bones.Length; i++)
            {
                boneTransforms[i] = skin.bones[i].localToWorldMatrix;
            }

            //Update bindedPoints
            for (int i = 0; i < bindedPoint.Length; i++)
            {
                Vector3 position = bindedPoint[i].origin;
                BoneWeight bw = bindedPoint[i].weights;

                Matrix4x4 delta0 = boneTransforms[bw.boneIndex0] * boneOrigins[bw.boneIndex0];
                Vector3 p0 = delta0.MultiplyPoint(position) * bw.weight0;

                Matrix4x4 delta1 = boneTransforms[bw.boneIndex1] * boneOrigins[bw.boneIndex1];
                Vector3 p1 = delta1.MultiplyPoint(position) * bw.weight1;

                Matrix4x4 delta2 = boneTransforms[bw.boneIndex2] * boneOrigins[bw.boneIndex2];
                Vector3 p2 = delta2.MultiplyPoint(position) * bw.weight2;

                Matrix4x4 delta3 = boneTransforms[bw.boneIndex3] * boneOrigins[bw.boneIndex3];
                Vector3 p3 = delta3.MultiplyPoint(position) * bw.weight3;

                Vector3 result = p0 + p1 + p2 + p3;
                transformedPoints[i] = result;
            }

            float3[] anchorPositions = new float3[3];

            //Notify anchors
            foreach (var anchor in anchors)
            {
                (int id, int length) key = anchor.PointsKey;
                for (int i = 0; i < key.length; i++)
                    anchorPositions[i] = transformedPoints[key.id + i];
                anchor.SetPositionFromBinding(anchorPositions);
            }
        }

        /// <summary>
        /// Returns bones to binding position. This is the position that anchors will use to bind to the object.
        /// </summary>
        public void GoToBindPose()
        {
            //Get components
            Mesh mesh = skin.sharedMesh;
            if (mesh == null)
                throw new System.NullReferenceException($"{gameObject.name} : Cannot be reset at bind pose because SkinnedMeshRenderer Mesh is null.");

            //Reset skin to bind pose
            for (int i = 0; i < skin.bones.Length; i++)
            {
                skin.bones[i].position = mesh.bindposes[i].inverse.MultiplyPoint(Vector3.zero);
                skin.bones[i].rotation = mesh.bindposes[i].inverse.rotation;
            }
        }

    }
}