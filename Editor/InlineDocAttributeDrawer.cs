using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
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

    private static readonly Dictionary<Assembly, CachedDocument> Documents = new();
    private static StyleSheet? styleSheet;
    private static GUIStyle? imguiStyle;

    private sealed class CachedDocument {
      public readonly string Path;
      public readonly DateTime LastWriteTimeUtc;
      public readonly long Length;
      public readonly XmlDocument Document;

      public CachedDocument(string path, DateTime lastWriteTimeUtc, long length, XmlDocument document) {
        Path = path;
        LastWriteTimeUtc = lastWriteTimeUtc;
        Length = length;
        Document = document;
      }
    }

    public override VisualElement CreatePropertyGUI(SerializedProperty property) {
      var root = new VisualElement();
      var comment = GetComment(fieldInfo);
      var hasDocumentationFile = HasDocumentationFile(fieldInfo);

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
      } else if (GetAssembly(fieldInfo) is { } assembly) {
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
      var comment = GetComment(fieldInfo);
      var assembly = GetAssembly(fieldInfo);
      if (comment is not { Length: > 0 } && assembly != null && !HasDocumentationFile(fieldInfo)) {
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
      var comment = GetComment(fieldInfo);
      if (!HasDocumentationFile(fieldInfo) && GetAssembly(fieldInfo) != null) {
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

    internal static string? GetComment(FieldInfo? field) {
      if (field?.DeclaringType == null) {
        return null;
      }

      var document = GetDocument(field.DeclaringType.Assembly);
      if (document == null) {
        return null;
      }

      var memberName = $"F:{GetDocumentationTypeName(field.DeclaringType)}.{field.Name}";
      var member = document.SelectSingleNode($"/doc/members/member[@name={ToXPathLiteral(memberName)}]");
      return member?.InnerXml;
    }

    internal static bool HasDocumentationFile(FieldInfo? field) {
      return GetAssembly(field) is { } assembly && GetDocumentationPath(assembly) != null;
    }

    internal static Assembly? GetAssembly(FieldInfo? field) {
      return field?.DeclaringType?.Assembly;
    }

    private static XmlDocument? GetDocument(Assembly assembly) {
      var documentationPath = GetDocumentationPath(assembly);
      if (documentationPath == null) {
        Documents.Remove(assembly);
        return null;
      }

      var file = new FileInfo(documentationPath);
      if (
        Documents.TryGetValue(assembly, out var cached) &&
        cached.Path == documentationPath &&
        cached.LastWriteTimeUtc == file.LastWriteTimeUtc &&
        cached.Length == file.Length
      ) {
        return cached.Document;
      }

      try {
        var document = new XmlDocument();
        document.Load(documentationPath);
        Documents[assembly] = new CachedDocument(
          documentationPath,
          file.LastWriteTimeUtc,
          file.Length,
          document
        );
        return document;
      } catch (XmlException) {
        Documents.Remove(assembly);
        return null;
      } catch (IOException) {
        Documents.Remove(assembly);
        return null;
      } catch (UnauthorizedAccessException) {
        Documents.Remove(assembly);
        return null;
      }
    }

    private static string? GetDocumentationPath(Assembly assembly) {
      var assemblyName = assembly.GetName().Name;
      if (!string.IsNullOrEmpty(assemblyName)) {
        var libraryPath = Path.GetFullPath(Path.Combine("Library", $"{assemblyName}.xml"));
        if (File.Exists(libraryPath)) {
          return libraryPath;
        }
      }

      var assemblyPath = assembly.Location;
      if (string.IsNullOrEmpty(assemblyPath)) {
        return null;
      }

      var adjacentPath = Path.ChangeExtension(assemblyPath, ".xml");
      return File.Exists(adjacentPath) ? adjacentPath : null;
    }

    private static string GetDocumentationTypeName(Type type) {
      return (type.FullName ?? type.Name).Replace('+', '.');
    }

    private static string ToXPathLiteral(string value) {
      if (!value.Contains("'")) {
        return $"'{value}'";
      }

      if (!value.Contains("\"")) {
        return $"\"{value}\"";
      }

      return $"concat('{value.Replace("'", "', \"'\", '")}')";
    }
  }
}
