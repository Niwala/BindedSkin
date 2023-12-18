using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Unity.Mathematics;

namespace SamsBackpack.BindedSkin
{
    public class BindedSkinAnchor : MonoBehaviour, IBindedSkinAnchor
    {
        public SkinData skinData;
        public SnapMethod snapMethod;

        public (int, int) PointsKey { get => _pointKey; set => _pointKey = value; }
        private (int, int) _pointKey;

        private float3 bindBarycentricCoords;
        private Quaternion triOriginRotation;

        public BindingPoint[] GetBindedVerticesIDs(Mesh mesh, Matrix4x4 meshTransform)
        {
            switch (snapMethod)
            {
                case SnapMethod.OnNearestVertex:
                    {
                        switch (skinData)
                        {
                            case SkinData.Position:
                                {
                                    (int id, float3 position) key = GetNearestVertex(transform.position, mesh, meshTransform);
                                    return new BindingPoint[] {
                                new BindingPoint(key.position, key.id) };
                                }
                            case SkinData.PositionAndRotation:
                                {
                                    (int id, float3 position) key = GetNearestVertex(transform.position, mesh, meshTransform);
                                    return new BindingPoint[] {
                                new BindingPoint(key.position, key.id),
                                new BindingPoint(key.position + new float3(0, 1, 0), key.id),
                                new BindingPoint(key.position + new float3(1, 0, 0), key.id) };
                                }
                        }
                        break;
                    }

                case SnapMethod.OnNearestTriangle:
                    {
                        switch (skinData)
                        {
                            case SkinData.Position:
                                {
                                    (int a, float3 aPos, int b, float3 bPos, int c, float3 cPos, float3 barycentric) key = GetNearestTriangle(transform.position, mesh, meshTransform);

                                    bindBarycentricCoords = key.barycentric;

                                    return new BindingPoint[] {
                                new BindingPoint(key.aPos, key.a) ,
                                new BindingPoint(key.bPos, key.b) ,
                                new BindingPoint(key.cPos, key.c) };
                                }
                            case SkinData.PositionAndRotation:
                                {
                                    (int a, float3 aPos, int b, float3 bPos, int c, float3 cPos, float3 barycentric) key = GetNearestTriangle(transform.position, mesh, meshTransform);

                                    bindBarycentricCoords = key.barycentric;
                                    triOriginRotation = Quaternion.LookRotation(key.bPos - key.aPos, key.cPos - key.aPos);

                                    return new BindingPoint[] {
                                new BindingPoint(key.aPos, key.a),
                                new BindingPoint(key.bPos, key.b),
                                new BindingPoint(key.cPos, key.c) };
                                }
                        }
                        break;
                    }
            }

            return new BindingPoint[0];
        }

        public void SetPositionFromBinding(params float3[] position)
        {
            switch (snapMethod)
            {
                case SnapMethod.OnNearestVertex:
                    switch (skinData)
                    {
                        case SkinData.Position:
                            {
                                transform.position = position[0];
                            }
                            break;

                        case SkinData.PositionAndRotation:
                            {
                                Vector3 up = position[1] - position[0];
                                Vector3 right = position[2] - position[0];
                                transform.position = position[0];
                                transform.rotation = Quaternion.LookRotation(up, right);
                            }
                            break;
                    }
                    break;


                case SnapMethod.OnNearestTriangle:
                    switch (skinData)
                    {
                        case SkinData.Position:
                            {
                                float3 center = position[0] * bindBarycentricCoords.x +
                                    position[1] * bindBarycentricCoords.y +
                                    position[2] * bindBarycentricCoords.z;

                                transform.position = center;
                            }
                            break;

                        case SkinData.PositionAndRotation:
                            {
                                //Position
                                float3 center = position[0] * bindBarycentricCoords.x +
                                    position[1] * bindBarycentricCoords.y +
                                    position[2] * bindBarycentricCoords.z;

                                transform.position = center;

                                //Rotation
                                Quaternion currentRotation = Quaternion.LookRotation(position[1] - position[0], position[2] - position[0]);
                                transform.rotation = currentRotation * Quaternion.Inverse(triOriginRotation);
                            }
                            break;
                    }
                    break;
            }
        }

        private (int, float3) GetNearestVertex(float3 point, Mesh mesh, Matrix4x4 meshTransform)
        {
            float minDist = Mathf.Infinity;
            int minID = 0;

            Vector3[] vertices = mesh.vertices;
            Vector3 p = meshTransform.inverse.MultiplyPoint(point);

            for (int i = 0; i < vertices.Length; i++)
            {
                float d = Vector3.Distance(vertices[i], p);
                if (d < minDist)
                {
                    minDist = d;
                    minID = i;
                }
            }

            return (minID, vertices[minID]);
        }

        private (int a, float3 aPos, int b, float3 bPos, int c, float3 cPos, float3 bary) GetNearestTriangle(float3 point, Mesh mesh, Matrix4x4 meshTransform)
        {
            float minDist = Mathf.Infinity;
            int minID = 0;
            float3 minBarycentric = default;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector3 p = meshTransform.inverse.MultiplyPoint(point);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                float3 a = vertices[triangles[i]];
                float3 b = vertices[triangles[i + 1]];
                float3 c = vertices[triangles[i + 2]];

                Plane plane = new Plane(a, b, c);
                float3 projectedPoint = plane.ClosestPointOnPlane(p);

                if (InTriangle(projectedPoint, a, b, c, out float3 barycentric))
                {
                    float dist = Vector3.Distance(p, projectedPoint);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        minID = i;
                        minBarycentric = barycentric;
                    }
                }
            }

            return (triangles[minID], vertices[triangles[minID]],
                triangles[minID + 1], vertices[triangles[minID + 1]],
                triangles[minID + 2], vertices[triangles[minID + 2]],
                minBarycentric);
        }

        private float3 Barycentric(float3 p, float3 a, float3 b, float3 c)
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

        private bool InTriangle(float3 p, float3 a, float3 b, float3 c, out float3 baryCentric)
        {
            baryCentric = Barycentric(p, a, b, c);
            return (baryCentric.x >= 0.0f && baryCentric.x < 1.0f && baryCentric.y >= 0.0f && baryCentric.y < 1.0f && baryCentric.z >= 0.0f && baryCentric.z < 1.0f);
        }

        public enum SkinData
        {
            Position,
            PositionAndRotation
        }

        public enum SnapMethod
        {
            OnNearestVertex,
            OnNearestTriangle,
        }

    }
}