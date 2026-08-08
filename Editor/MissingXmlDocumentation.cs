using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;
using ReflectionAssembly = System.Reflection.Assembly;

namespace EffortStar.EditorXmlDocs.Editor {
  internal static class MissingXmlDocumentation {
    public const float ImguiHeight = 30f;

    public static VisualElement Create(string attributeName, ReflectionAssembly assembly) {
      var asmdefPath = GetAsmdefPath(assembly);
      var row = new VisualElement();
      row.style.flexDirection = FlexDirection.Row;
      row.style.alignItems = Align.Center;

      var message = new HelpBox(GetMessage(attributeName, assembly, asmdefPath), HelpBoxMessageType.Warning);
      message.style.flexGrow = 1f;
      row.Add(message);

      var button = new Button(() => Fix(assembly, asmdefPath)) {
        text = "Fix"
      };
      button.SetEnabled(asmdefPath != null);
      row.Add(button);
      return row;
    }

    public static void Draw(
      Rect position,
      string attributeName,
      ReflectionAssembly assembly
    ) {
      var asmdefPath = GetAsmdefPath(assembly);
      GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

      const float buttonWidth = 42f;
      const float spacing = 4f;
      var buttonPosition = new Rect(
        position.xMax - buttonWidth - spacing,
        position.y + spacing,
        buttonWidth,
        EditorGUIUtility.singleLineHeight
      );
      var labelPosition = new Rect(
        position.x + spacing,
        position.y + spacing,
        position.width - buttonWidth - spacing * 3f,
        EditorGUIUtility.singleLineHeight
      );

      GUI.Label(labelPosition, GetMessage(attributeName, assembly, asmdefPath));
      using (new EditorGUI.DisabledScope(asmdefPath == null)) {
        if (GUI.Button(buttonPosition, "Fix")) {
          Fix(assembly, asmdefPath);
        }
      }
    }

    static string? GetAsmdefPath(ReflectionAssembly assembly) {
      var assemblyName = assembly.GetName().Name;
      return string.IsNullOrEmpty(assemblyName)
        ? null
        : CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
    }

    static string GetMessage(
      string attributeName,
      ReflectionAssembly assembly,
      string? asmdefPath
    ) {
      var asmdefName = asmdefPath == null
        ? $"{assembly.GetName().Name}.asmdef"
        : Path.GetFileName(asmdefPath);
      return $"[{attributeName}] did not find an XML file. {asmdefName} may not be configured.";
    }

    static void Fix(ReflectionAssembly assembly, string? asmdefPath) {
      if (asmdefPath == null) {
        return;
      }

      var assemblyName = assembly.GetName().Name;
      if (string.IsNullOrEmpty(assemblyName)) {
        return;
      }

      var asmdefDirectory = Path.GetDirectoryName(asmdefPath);
      if (string.IsNullOrEmpty(asmdefDirectory)) {
        return;
      }

      var responseFilePath = Path.Combine(asmdefDirectory, "csc.rsp");
      var lines = File.Exists(responseFilePath)
        ? File.ReadAllLines(responseFilePath).ToList()
        : new List<string>();
      var documentationArgument = $"-doc:Library/{assemblyName}.xml";
      var documentationIndex = lines.FindIndex(IsDocumentationArgument);

      lines.RemoveAll(IsDocumentationArgument);
      if (documentationIndex < 0 || documentationIndex > lines.Count) {
        lines.Add(documentationArgument);
      } else {
        lines.Insert(documentationIndex, documentationArgument);
      }

      if (!lines.Any(DisablesMissingDocumentationWarning)) {
        lines.Add("-nowarn:1591");
      }

      File.WriteAllLines(responseFilePath, lines);
      AssetDatabase.ImportAsset(
        responseFilePath.Replace('\\', '/'),
        ImportAssetOptions.ForceSynchronousImport
      );
      CompilationPipeline.RequestScriptCompilation();
    }

    static bool IsDocumentationArgument(string line) {
      var trimmed = line.TrimStart();
      return
        trimmed.StartsWith("-doc:", StringComparison.OrdinalIgnoreCase) ||
        trimmed.StartsWith("/doc:", StringComparison.OrdinalIgnoreCase);
    }

    static bool DisablesMissingDocumentationWarning(string line) {
      var trimmed = line.Trim();
      var separator = trimmed.IndexOf(':');
      if (separator < 0) {
        return false;
      }

      var option = trimmed[..separator];
      if (
        !option.Equals("-nowarn", StringComparison.OrdinalIgnoreCase) &&
        !option.Equals("/nowarn", StringComparison.OrdinalIgnoreCase)
      ) {
        return false;
      }

      return trimmed[(separator + 1)..]
        .Split(',', ';')
        .Any(value => value.Trim() == "1591");
    }
  }
}
