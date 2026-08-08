using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EffortStar.EditorXmlDocs.Editor {
  [CustomPropertyDrawer(typeof(InlineDocAttribute))]
  public sealed class InlineDocAttributeDrawer : PropertyDrawer {
    private const string StyleSheetPath =
      "Packages/games.effortstar.editor-xml-docs/Editor/InlineDoc.uss";
    private const string InlineDocClass = "effortstar-inline-doc";
    internal const string NoDocComment = "No doc comment.";

    private static StyleSheet? styleSheet;
    private static GUIStyle? imguiStyle;

    public override VisualElement CreatePropertyGUI(SerializedProperty property) {
      var root = new VisualElement();
      var comment = DocUtility.GetComment(fieldInfo);
      var hasDocumentationFile = DocUtility.HasDocumentationFile(fieldInfo);

      if (comment is { Length: > 0 } || hasDocumentationFile) {
        var text = comment is { Length: > 0 }
          ? XmlRichTextConverter.Convert(comment)
          : NoDocComment;
        var commentLabel = new Label(text) {
          enableRichText = true
        };
        commentLabel.AddToClassList(InlineDocClass);
        commentLabel.style.whiteSpace = WhiteSpace.PreWrap;
        root.Add(commentLabel);
      } else if (DocUtility.GetAssembly(fieldInfo) is { } assembly) {
        root.Add(MissingXmlDocumentation.Create("InlineDoc", assembly));
      }

      styleSheet ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
      if (styleSheet != null) {
        root.styleSheets.Add(styleSheet);
      }

      root.Add(new PropertyField(property, preferredLabel));
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
        MissingXmlDocumentation.Draw(warningPosition, "InlineDoc", assembly);

        var propertyAfterWarningPosition = new Rect(
          position.x,
          warningPosition.yMax + EditorGUIUtility.standardVerticalSpacing,
          position.width,
          EditorGUI.GetPropertyHeight(property, label, includeChildren: true)
        );
        EditorGUI.PropertyField(
          propertyAfterWarningPosition,
          property,
          label,
          includeChildren: true
        );
        return;
      }

      if (comment is not { Length: > 0 }) {
        comment = NoDocComment;
      }

      var content = new GUIContent(XmlRichTextConverter.Convert(comment));
      var commentHeight = GetCommentHeight(content, position.width);
      var commentPosition = new Rect(position.x, position.y, position.width, commentHeight);
      GUI.Label(commentPosition, content, GetImguiStyle());

      var propertyPosition = new Rect(
        position.x,
        commentPosition.yMax + EditorGUIUtility.standardVerticalSpacing,
        position.width,
        EditorGUI.GetPropertyHeight(property, label, includeChildren: true)
      );
      EditorGUI.PropertyField(propertyPosition, property, label, includeChildren: true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
      var propertyHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
      var comment = DocUtility.GetComment(fieldInfo);
      if (
        !DocUtility.HasDocumentationFile(fieldInfo) &&
        DocUtility.GetAssembly(fieldInfo) != null
      ) {
        return
          MissingXmlDocumentation.ImguiHeight +
          EditorGUIUtility.standardVerticalSpacing +
          propertyHeight;
      }

      if (comment is not { Length: > 0 }) {
        comment = NoDocComment;
      }

      var width = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 40f);
      var content = new GUIContent(XmlRichTextConverter.Convert(comment));
      return
        GetCommentHeight(content, width) +
        EditorGUIUtility.standardVerticalSpacing +
        propertyHeight;
    }

    private static float GetCommentHeight(GUIContent content, float width) {
      return GetImguiStyle().CalcHeight(content, width);
    }

    private static GUIStyle GetImguiStyle() {
      if (imguiStyle != null) {
        return imguiStyle;
      }

      imguiStyle = new GUIStyle(EditorStyles.helpBox) {
        padding = new RectOffset(2, 2, 2, 2),
        richText = true,
        wordWrap = true
      };
      return imguiStyle;
    }

  }
}
