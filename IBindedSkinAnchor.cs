using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Unity.Mathematics;

namespace SamsBackpack.BindedSkin
{
    public interface IBindedSkinAnchor
    {
        public (int, int) PointsKey { get; set; }

        public abstract BindingPoint[] GetBindedVerticesIDs(Mesh mesh, Matrix4x4 meshTransform);

        public abstract void SetPositionFromBinding(params float3[] position);
    }
}