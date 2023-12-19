using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Jobs;

using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;

namespace SamsBackpack.BindedSkin
{
    /// <summary>
    /// The work of this component is mainly divided into two jobs.
    /// Anchor transforms are distributed to one or more points, depending on their settings.
    ///  - The first job moves all points based on bone movement.
    ///  - The second job applies the result of the move to the anchor transforms.
    ///  
    /// This component is not designed to dynamically change anchor lists. 
    /// If you need to refresh the list, use the RefreshRuntimeBinding() function after parenting the SkinAnchors to the object. 
    /// Please note, however, that this can be a time-consuming operation.
    /// </summary>
    public class BindedSkin : MonoBehaviour
    {
        private const int jobBatchSize = 32;

        [HideInInspector, SerializeField]
        public SkinnedMeshRenderer skin;

        //Runtime data
        [HideInInspector]
        public bool hasRuntimeData;
        private IBindedSkinAnchor[] anchors;
        public int anchorCount => anchors == null ? 0 : anchors.Length;

        //Jobs stuff
        private NativeArray<BindingPointInfo> bindedPoint;
        private NativeArray<float3> transformedPoints;
        private NativeArray<float4x4> boneTransforms;
        private NativeArray<float4x4> boneOrigins;
        private NativeArray<BindingTransformInfo> bindingInfo;
        private TransformAccessArray transformArray;
        private JobHandle skinPointsJobHandle;
        private JobHandle transformJobHandle;

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


            //Load anchors childs
            anchors = GetComponentsInChildren<IBindedSkinAnchor>();
            if (anchors.Length == 0)
                return;


            //Reset to bind pose (Required for anchors to connect to the right vertices)
            GoToBindPose();


            //Load Mesh data
            Matrix4x4 l2w = skin.transform.localToWorldMatrix;
            Vector3[] vertices = skin.sharedMesh.vertices;
            float3[] worldSpaceVertices = new float3[vertices.Length];
            for (int i = 0; i < worldSpaceVertices.Length; i++)
            {
                worldSpaceVertices[i] = l2w.MultiplyPoint(vertices[i]);
            }
            int[] triangles = skin.sharedMesh.triangles;
            BoneWeight[] meshWeights = skin.sharedMesh.boneWeights;


            //Converts all anchors NativeArray<BindingData> for fast processing.
            List<BindingPointInfo> tempList = new List<BindingPointInfo>();
            List<BindingTransformInfo> tempInfo = new List<BindingTransformInfo>();
            List<Transform> tempTransforms = new List<Transform>();


            foreach (var anchor in anchors)
            {
                BindingTransformInfo info = anchor.GetBindingTransform();
                Transform transform = anchor.GetTransform();
                info.id = tempList.Count;

                switch (info.snapMethod)
                {
                    case SnapMethod.OnNearestVertex:
                        switch (info.skinData)
                        {
                            case SkinData.Position:
                                {
                                    (int nearestVertexID, float3 nearestVertexPos) = GetNearestVertexID(transform.position);
                                    tempList.Add(new BindingPointInfo(nearestVertexPos, meshWeights[nearestVertexID]));
                                }
                                break;

                            case SkinData.PositionAndRotation:
                                {
                                    (int nearestVertexID, float3 nearestVertexPos) = GetNearestVertexID(transform.position);
                                    tempList.Add(new BindingPointInfo(nearestVertexPos, meshWeights[nearestVertexID]));
                                    tempList.Add(new BindingPointInfo(nearestVertexPos + new float3(0, 1, 0), meshWeights[nearestVertexID]));
                                    tempList.Add(new BindingPointInfo(nearestVertexPos + new float3(1, 0, 0), meshWeights[nearestVertexID]));
                                    info.rotation = quaternion.LookRotationSafe(new float3(0, 1, 0), new float3(1, 0, 0));
                                }
                                break;
                        }
                        break;

                    case SnapMethod.OnNearestTriangle:
                        {
                            (int a, float3 aPos, int b, float3 bPos, int c, float3 cPos, float3 barycentric) = GetNearestTriangleID(transform.position);
                            tempList.Add(new BindingPointInfo(aPos, meshWeights[a]));
                            tempList.Add(new BindingPointInfo(bPos, meshWeights[b]));
                            tempList.Add(new BindingPointInfo(cPos, meshWeights[c]));
                            info.barycentric = barycentric;
                            info.rotation = quaternion.LookRotationSafe(bPos - aPos, cPos - aPos);
                        }
                        break;
                }

                tempInfo.Add(info);
                tempTransforms.Add(transform);
            }

            bindedPoint = new NativeArray<BindingPointInfo>(tempList.ToArray(), Allocator.Persistent);
            transformedPoints = new NativeArray<float3>(tempList.Count, Allocator.Persistent);
            bindingInfo = new NativeArray<BindingTransformInfo>(tempInfo.ToArray(), Allocator.Persistent);
            transformArray = new TransformAccessArray(tempTransforms.ToArray());

            //Build bone arrays
             
            Matrix4x4[] skinBindPoses = skin.sharedMesh.bindposes;
            boneTransforms = new NativeArray<float4x4>(skin.bones.Length, Allocator.Persistent);
            boneOrigins = new NativeArray<float4x4>(skinBindPoses.Length, Allocator.Persistent);
            for (int i = 0; i < skin.sharedMesh.bindposes.Length; i++)
            {
                boneOrigins[i] = skinBindPoses[i];
            }

            hasRuntimeData = true;

            (int, float3) GetNearestVertexID(float3 position)
            {
                float minDist = Mathf.Infinity;
                int minID = 0;

                for (int i = 0; i < worldSpaceVertices.Length; i++)
                {
                    float d = Vector3.Distance(worldSpaceVertices[i], position);
                    if (d < minDist)
                    {
                        minDist = d;
                        minID = i;
                    }
                }

                return (minID, worldSpaceVertices[minID]);
            }

            (int, float3, int, float3, int, float3, float3) GetNearestTriangleID(float3 position)
            {
                float minDist = Mathf.Infinity;
                int minID = 0;
                float3 minBarycentric = default;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    float3 a = worldSpaceVertices[triangles[i]];
                    float3 b = worldSpaceVertices[triangles[i + 1]];
                    float3 c = worldSpaceVertices[triangles[i + 2]];

                    Plane plane = new Plane(a, b, c);
                    float3 projectedPoint = plane.ClosestPointOnPlane(position);

                    if (InTriangle(projectedPoint, a, b, c, out float3 barycentric))
                    {
                        float dist = math.distance(position, projectedPoint);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            minID = i;
                            minBarycentric = barycentric;
                        }
                    }
                }

                return (triangles[minID], worldSpaceVertices[triangles[minID]],
                    triangles[minID + 1], worldSpaceVertices[triangles[minID + 1]],
                    triangles[minID + 2], worldSpaceVertices[triangles[minID + 2]],
                    minBarycentric);
            }
        }

        /// <summary>
        /// Disposes of all data optimized for runtime processing.
        /// </summary>
        public void ReleaseRuntimeBinding()
        {
            hasRuntimeData = false;

            bindedPoint.Dispose();
            bindingInfo.Dispose();
            transformedPoints.Dispose();
            boneTransforms.Dispose();
            boneOrigins.Dispose();
            transformArray.Dispose();
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


            //Execute skinning via job
            SkinBindingJob skinJob = new SkinBindingJob()
            {
                boneTransforms = boneTransforms,
                boneOrigins = boneOrigins,
                skinnedPointInfo = bindedPoint,
                skinnedPoints = transformedPoints
            };
            skinPointsJobHandle = skinJob.Schedule(bindedPoint.Length, jobBatchSize, transformJobHandle);


            //Apply skinning to transforms
            SkinTransformJob transformJob = new SkinTransformJob()
            {
                transformedPoints = transformedPoints,
                bindingTransforms = bindingInfo
            };
            transformJobHandle = transformJob.Schedule(transformArray, skinPointsJobHandle);


            //Execute jobs
            transformJobHandle.Complete();
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

        /// <summary>
        /// Returns the barycentric coordinate of point p in the triangle [a, b, c]
        /// </summary>
        private static float3 Barycentric(float3 p, float3 a, float3 b, float3 c)
        {
            float3 v0 = b - a, v1 = c - a, v2 = p - a;
            float d00 = math.dot(v0, v0);
            float d01 = math.dot(v0, v1);
            float d11 = math.dot(v1, v1);
            float d20 = math.dot(v2, v0);
            float d21 = math.dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1.0f - v - w;
            return new float3(u, v, w);
        }

        /// <summary>
        /// Check if the point p is triangle [a, b, c] and return the barycentric coordinate 
        /// </summary>
        private static bool InTriangle(float3 p, float3 a, float3 b, float3 c, out float3 baryCentric)
        {
            baryCentric = Barycentric(p, a, b, c);
            return (baryCentric.x >= 0.0f && baryCentric.x < 1.0f && baryCentric.y >= 0.0f && baryCentric.y < 1.0f && baryCentric.z >= 0.0f && baryCentric.z < 1.0f);
        }

        /// <summary>
        /// This job moves all skinned points based on bones.
        /// </summary>
        [BurstCompile(CompileSynchronously = true)]
        private struct SkinBindingJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4x4> boneTransforms;
            [ReadOnly] public NativeArray<float4x4> boneOrigins;
            [ReadOnly] public NativeArray<BindingPointInfo> skinnedPointInfo;
            [WriteOnly] public NativeArray<float3> skinnedPoints;

            //bindedPoint.Length
            public void Execute(int i)
            {
                float4 position = new float4(skinnedPointInfo[i].origin, 1.0f);
                BoneWeight bw = skinnedPointInfo[i].weights;

                float4x4 delta0 = math.mul(boneTransforms[bw.boneIndex0], boneOrigins[bw.boneIndex0]);
                float3 p0 = math.mul(delta0, position).xyz * bw.weight0;

                float4x4 delta1 = math.mul(boneTransforms[bw.boneIndex1], boneOrigins[bw.boneIndex1]);
                float3 p1 = math.mul(delta1, position).xyz * bw.weight1;

                float4x4 delta2 = math.mul(boneTransforms[bw.boneIndex2], boneOrigins[bw.boneIndex2]);
                float3 p2 = math.mul(delta2, position).xyz * bw.weight2;

                float4x4 delta3 = math.mul(boneTransforms[bw.boneIndex3], boneOrigins[bw.boneIndex3]);
                float3 p3 = math.mul(delta3, position).xyz * bw.weight3;

                float3 result = p0 + p1 + p2 + p3;
                skinnedPoints[i] = result;
            }
        }

        /// <summary>
        /// This job moves anchors on the basis of one or more points, depending on the anchor settings.
        /// </summary>
        [BurstCompile(CompileSynchronously = true)]
        private struct SkinTransformJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<float3> transformedPoints;
            [ReadOnly] public NativeArray<BindingTransformInfo> bindingTransforms;

            public void Execute(int i, TransformAccess transformAccess)
            {
                BindingTransformInfo info = bindingTransforms[i];

                switch (info.snapMethod)
                {
                    case SnapMethod.OnNearestVertex:
                        switch (info.skinData)
                        {
                            case SkinData.Position:
                                {
                                    transformAccess.position = transformedPoints[info.id];
                                }
                                break;

                            case SkinData.PositionAndRotation:
                                {
                                    quaternion currentRotation = quaternion.LookRotationSafe(transformedPoints[info.id + 1] - transformedPoints[info.id], transformedPoints[info.id + 2] - transformedPoints[info.id]);
                                    transformAccess.SetPositionAndRotation(transformedPoints[info.id], math.mul(currentRotation, math.inverse(info.rotation)));
                                }
                                break;
                        }
                        break;


                    case SnapMethod.OnNearestTriangle:
                        switch (info.skinData)
                        {
                            case SkinData.Position:
                                {
                                    transformAccess.position = transformedPoints[info.id] * info.barycentric.x +
                                        transformedPoints[info.id + 1] * info.barycentric.y +
                                        transformedPoints[info.id + 2] * info.barycentric.z;
                                }
                                break;

                            case SkinData.PositionAndRotation:
                                {
                                    float3 center = transformedPoints[info.id] * info.barycentric.x +
                                        transformedPoints[info.id + 1] * info.barycentric.y +
                                        transformedPoints[info.id + 2] * info.barycentric.z;
                                    quaternion currentRotation = quaternion.LookRotationSafe(transformedPoints[info.id + 1] - transformedPoints[info.id], transformedPoints[info.id + 2] - transformedPoints[info.id]);
                                    transformAccess.SetPositionAndRotation(center, math.mul(currentRotation, math.inverse(info.rotation)));
                                }
                                break;
                        }
                        break;
                }
            }

        }
    }
}