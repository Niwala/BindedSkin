using UnityEngine;

namespace SamsBackpack.BindedSkin
{
    public interface IBindedSkinAnchor
    {
        public abstract BindingTransformInfo GetBindingTransform();

        public abstract Transform GetTransform();
    }
}