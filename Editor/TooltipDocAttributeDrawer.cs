using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EffortStar.EditorXmlDocs.Editor {
  [CustomPropertyDrawer(typeof(TooltipDocAttribute))]
  public sealed class TooltipDocAttributeDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty property) {
      var propertyField = new PropertyField(property, preferredLabel);
      var richText = RichTextCache.Get(fieldInfo);

      if (richText != null) {
        propertyField.tooltip = richText;
      }

      if (richText != null) {
        return propertyField;
      }

      if (DocUtility.GetAssembly(fieldInfo) is not { } assembly) {
        return propertyField;
      }

      var root = new VisualElement();
      root.Add(MissingXmlDocumentation.Create("TooltipDoc", assembly));
      root.Add(propertyField);
      return root;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var richText = RichTextCache.Get(fieldInfo);
      if (
        richText == null &&
        DocUtility.GetAssembly(fieldInfo) is { } assembly
      ) {
        var warningPosition = new Rect(
          position.x,
          position.y,
          position.width,
          MissingXmlDocumentation.ImguiHeight
        );
        MissingXmlDocumentation.Draw(warningPosition, "TooltipDoc", assembly);
        position.y = warningPosition.yMax + EditorGUIUtility.standardVerticalSpacing;
        position.height = EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
      }

      var propertyLabel = richText != null
        ? new GUIContent(label.text, label.image, richText)
        : label;

      EditorGUI.PropertyField(position, property, propertyLabel, includeChildren: true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var propertyHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
      if (
        RichTextCache.Get(fieldInfo) == null &&
        DocUtility.GetAssembly(fieldInfo) != null
      ) {
        return
          MissingXmlDocumentation.ImguiHeight +
          EditorGUIUtility.standardVerticalSpacing +
          propertyHeight;
      }

      return propertyHeight;
    }
  }
}
