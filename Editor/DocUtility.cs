using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace EffortStar.EditorXmlDocs.Editor {
  internal static class DocUtility {
    private static readonly Dictionary<Assembly, CachedDocument> _documents = new();

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
        _documents.Remove(assembly);
        return null;
      }

      var file = new FileInfo(documentationPath);
      if (
        _documents.TryGetValue(assembly, out var cached) &&
        cached.Path == documentationPath &&
        cached.LastWriteTimeUtc == file.LastWriteTimeUtc &&
        cached.Length == file.Length
      ) {
        return cached.Document;
      }

      try {
        var document = new XmlDocument();
        document.Load(documentationPath);
        _documents[assembly] = new CachedDocument(
          documentationPath,
          file.LastWriteTimeUtc,
          file.Length,
          document
        );
        return document;
      } catch (XmlException) {
        _documents.Remove(assembly);
        return null;
      } catch (IOException) {
        _documents.Remove(assembly);
        return null;
      } catch (UnauthorizedAccessException) {
        _documents.Remove(assembly);
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
