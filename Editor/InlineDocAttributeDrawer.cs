using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EffortStar.EditorXmlDocs.Editor {
  [CustomPropertyDrawer(typeof(InlineDocAttribute))]
  public sealed class InlineDocAttributeDrawer : PropertyDrawer {
    const string StyleSheetPath =
      "Packages/games.effortstar.editor-xml-docs/Editor/InlineDoc.uss";
    const string InlineDocClass = "effortstar-inline-doc";

    static StyleSheet? _styleSheet;
    static GUIStyle? _imguiStyle;

    public override VisualElement CreatePropertyGUI(SerializedProperty property) {
      var root = new VisualElement();
      var richText = RichTextCache.Get(fieldInfo);

      if (richText != null) {
        var commentLabel = new Label(richText) {
          enableRichText = true
        };
        commentLabel.AddToClassList(InlineDocClass);
        commentLabel.style.whiteSpace = WhiteSpace.PreWrap;
        root.Add(commentLabel);
      } else if (DocUtility.GetAssembly(fieldInfo) is { } assembly) {
        root.Add(MissingXmlDocumentation.Create("InlineDoc", assembly));
      }

      _styleSheet ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
      if (_styleSheet != null) {
        root.styleSheets.Add(_styleSheet);
      }

      root.Add(new PropertyField(property, preferredLabel));
      return root;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
      var richText = RichTextCache.Get(fieldInfo);
      if (richText == null) {
        if (DocUtility.GetAssembly(fieldInfo) is not { } assembly) {
          EditorGUI.PropertyField(position, property, label, includeChildren: true);
          return;
        }

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

      var content = new GUIContent(richText);
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
      var richText = RichTextCache.Get(fieldInfo);
      if (richText == null) {
        return DocUtility.GetAssembly(fieldInfo) != null
          ? MissingXmlDocumentation.ImguiHeight +
            EditorGUIUtility.standardVerticalSpacing +
            propertyHeight
          : propertyHeight;
      }

      var width = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 40f);
      var content = new GUIContent(richText);
      return
        GetCommentHeight(content, width) +
        EditorGUIUtility.standardVerticalSpacing +
        propertyHeight;
    }

    static float GetCommentHeight(GUIContent content, float width) {
      return GetImguiStyle().CalcHeight(content, width);
    }

    static GUIStyle GetImguiStyle() {
      if (_imguiStyle != null) {
        return _imguiStyle;
      }

      _imguiStyle = new GUIStyle(EditorStyles.helpBox) {
        padding = new RectOffset(2, 2, 2, 2),
        richText = true,
        wordWrap = true
      };
      return _imguiStyle;
    }

  }
}
