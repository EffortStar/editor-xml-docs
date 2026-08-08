using System.Collections.Generic;
using System.Reflection;

namespace EffortStar.EditorXmlDocs.Editor {
  internal static class RichTextCache {
    const string NoDocComment = "No doc comment.";

    static readonly Dictionary<FieldInfo, string?> _cache = new();

    internal static string? Get(FieldInfo? field) {
      if (field == null) {
        return null;
      }

      if (_cache.TryGetValue(field, out var cached)) {
        return cached;
      }

      var comment = DocUtility.GetComment(field);
      var richText = comment is { Length: > 0 }
        ? XmlRichTextConverter.Convert(comment)
        : DocUtility.HasDocumentationFile(field)
          ? NoDocComment
          : null;

      _cache[field] = richText;
      return richText;
    }
  }
}
