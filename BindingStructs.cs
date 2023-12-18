using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Unity.Mathematics;

namespace SamsBackpack.BindedSkin
{
    public struct BindingPoint
    {
        public float3 position;
        public int skinPointreference;

        public BindingPoint(float3 position, int skinPointreference)
        {
            this.position = position;
            this.skinPointreference = skinPointreference;
        }
    }

    public struct BindingData
    {
        public float3 origin;
        public BoneWeight weights;
        public int anchorID;
        public int pointID;

        public BindingData(float3 origin, BoneWeight weights, int anchorID, int pointID)
        {
            this.origin = origin;
            this.weights = weights;
            this.anchorID = anchorID;
            this.pointID = pointID;
        }
    }
}