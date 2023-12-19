using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace SamsBackpack.BindedSkin
{
    [CustomEditor(typeof(BindedSkin))]
    public class BindedSkinEditor : Editor
    {
        //Components
        private BindedSkin bindedSkin;
        private SkinnedMeshRenderer skin => bindedSkin.skin;

        //UI
        private VisualElement noSkinSection;
        private VisualElement skinSection;
        private PropertyField skinField;
        private Button gotToBindPoseBtn;

        public override VisualElement CreateInspectorGUI()
        {
            //Components
            VisualElement inspector = new VisualElement();
            bindedSkin = target as BindedSkin;

            //Skin field
            skinField = new PropertyField();
            skinField.RegisterCallback<GeometryChangedEvent>(ChangeLeftSpacing);
            skinField.BindProperty(serializedObject.FindProperty(nameof(bindedSkin.skin)));
            skinField.RegisterValueChangeCallback((SerializedPropertyChangeEvent e) => LoadSkinComponents());
            inspector.Add(skinField);

            //Sections
            noSkinSection = new VisualElement();
            PopulateNoSkinSection(noSkinSection);
            inspector.Add(noSkinSection);

            skinSection = new VisualElement();
            PopulateSkinSectionGUI(skinSection);
            inspector.Add(skinSection);

            LoadSkinComponents();

            return inspector;
        }

        private void PopulateNoSkinSection(VisualElement section)
        {
            //Message
            HelpBox msg = new HelpBox("", HelpBoxMessageType.Warning);
            if (skin == null)
                msg.text = "No skinMeshRenderer found on this object or its children.";
            else
                msg.text = "No mesh found on the skinMeshRenderer.";
            section.Add(msg);

            //Refresh button
            Button button = new Button() { text = "Search in children" };
            button.clicked += SearchSkinInChildren;
            section.Add(button);
        }

        private void PopulateSkinSectionGUI(VisualElement section)
        {
            //Go to bind pose button
            gotToBindPoseBtn = new Button() { text = "Go to bind pose" };
            gotToBindPoseBtn.style.marginRight = -3;
            gotToBindPoseBtn.clicked += GoToBindPose;
            section.Add(gotToBindPoseBtn);
        }

        private void SearchSkinInChildren()
        {
            SkinnedMeshRenderer skin = bindedSkin.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null)
            {
                serializedObject.FindProperty(nameof(bindedSkin.skin)).objectReferenceValue = skin;
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void LoadSkinComponents()
        {
            bool validSkin = skin != null && skin.sharedMesh != null;
            skinSection.style.display = validSkin ? DisplayStyle.Flex : DisplayStyle.None;
            noSkinSection.style.display = validSkin ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void ChangeLeftSpacing(GeometryChangedEvent e)
        {
            Label label = skinField.Q<Label>();
            if (label == null)
                return;
            gotToBindPoseBtn.style.marginLeft = label.style.width.value.value + 4;
        }

        private void GoToBindPose()
        {
            bindedSkin.GoToBindPose();
        }
    }
}