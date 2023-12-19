using UnityEngine;
using Unity.Mathematics;

namespace SamsBackpack.BindedSkin
{
    public struct BindingPointInfo
    {
        public float3 origin;
        public BoneWeight weights;

        public BindingPointInfo(float3 origin, BoneWeight weights)
        {
            this.origin = origin;
            this.weights = weights;
        }
    }

    public struct BindingTransformInfo
    {
        public SkinData skinData;
        public SnapMethod snapMethod;

        public int id;
        public float3 barycentric;
        public quaternion rotation;
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