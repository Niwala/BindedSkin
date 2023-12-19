using UnityEngine;

namespace SamsBackpack.BindedSkin
{
    public class BindedSkinAnchor : MonoBehaviour, IBindedSkinAnchor
    {
        public SkinData skinData;
        public SnapMethod snapMethod;

        public BindingTransformInfo GetBindingTransform()
        {
            return new BindingTransformInfo()
            {
                skinData = skinData,
                snapMethod = snapMethod,
            };
        }

        public Transform GetTransform()
        {
            return transform;
        }
    }
}