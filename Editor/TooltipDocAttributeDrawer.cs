using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EffortStar.EditorXmlDocs.Editor {
  [CustomPropertyDrawer(typeof(TooltipDocAttribute))]
  public sealed class TooltipDocAttributeDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty property) {
      var propertyField = new PropertyField(property, preferredLabel);
      var comment = DocUtility.GetComment(fieldInfo);
      var hasDocumentationFile = DocUtility.HasDocumentationFile(fieldInfo);

      if (comment is { Length: > 0 }) {
        propertyField.tooltip = XmlRichTextConverter.Convert(comment);
      } else if (hasDocumentationFile) {
        propertyField.tooltip = InlineDocAttributeDrawer.NoDocComment;
      }

      if (
        DocUtility.GetAssembly(fieldInfo) is not { } assembly ||
        hasDocumentationFile
      ) {
        return propertyField;
      }

      var root = new VisualElement();
      root.Add(MissingXmlDocumentation.Create("TooltipDoc", assembly));
      root.Add(propertyField);
      return root;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var comment = DocUtility.GetComment(fieldInfo);
      var assembly = DocUtility.GetAssembly(fieldInfo);
      if (
        comment is not { Length: > 0 } &&
        assembly != null &&
        !DocUtility.HasDocumentationFile(fieldInfo)
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

      var propertyLabel = comment is { Length: > 0 }
        ? new GUIContent(label.text, label.image, XmlRichTextConverter.Convert(comment))
        : DocUtility.HasDocumentationFile(fieldInfo)
          ? new GUIContent(label.text, label.image, InlineDocAttributeDrawer.NoDocComment)
          : label;

      EditorGUI.PropertyField(position, property, propertyLabel, includeChildren: true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var propertyHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
      if (
        DocUtility.GetAssembly(fieldInfo) != null &&
        !DocUtility.HasDocumentationFile(fieldInfo)
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
