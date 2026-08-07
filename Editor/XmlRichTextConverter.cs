using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Xml;

namespace EffortStar.EditorXmlDocs.Editor {
  internal static class XmlRichTextConverter {
    private static readonly HashSet<string> ParagraphElements = new(StringComparer.OrdinalIgnoreCase) {
      "description",
      "example",
      "exception",
      "item",
      "list",
      "para",
      "remarks",
      "returns",
      "summary",
      "term",
      "value"
    };

    public static string Convert(string xml) {
      if (string.IsNullOrWhiteSpace(xml)) {
        return string.Empty;
      }

      var document = new XmlDocument {
        PreserveWhitespace = true
      };

      try {
        document.LoadXml($"<root>{xml}</root>");
      } catch (XmlException) {
        var fallback = new StringBuilder();
        AppendText(fallback, xml.Trim());
        return Normalize(fallback.ToString());
      }

      TrimBoundaryWhitespace(document.DocumentElement!);

      var output = new StringBuilder();

      foreach (XmlNode child in document.DocumentElement!.ChildNodes) {
        AppendNode(output, child);
      }

      return Normalize(output.ToString());
    }

    private static void TrimBoundaryWhitespace(XmlElement root) {
      var textNodes = new List<XmlNode>();
      CollectTextNodes(root, textNodes);

      foreach (var textNode in textNodes) {
        var trimmed = textNode.Value?.TrimStart() ?? string.Empty;
        textNode.Value = trimmed;
        if (trimmed.Length > 0) {
          break;
        }
      }

      for (var index = textNodes.Count - 1; index >= 0; index--) {
        var trimmed = textNodes[index].Value?.TrimEnd() ?? string.Empty;
        textNodes[index].Value = trimmed;
        if (trimmed.Length > 0) {
          break;
        }
      }
    }

    private static void CollectTextNodes(XmlNode node, List<XmlNode> output) {
      if (
        node is XmlText or
        XmlWhitespace or
        XmlSignificantWhitespace or
        XmlCDataSection
      ) {
        output.Add(node);
        return;
      }

      foreach (XmlNode child in node.ChildNodes) {
        CollectTextNodes(child, output);
      }
    }

    private static void AppendNode(StringBuilder output, XmlNode node) {
      switch (node) {
        case XmlText text:
          AppendText(output, text.Value);
          return;
        case XmlWhitespace whitespace:
          AppendText(output, whitespace.Value);
          return;
        case XmlSignificantWhitespace whitespace:
          AppendText(output, whitespace.Value);
          return;
        case XmlCDataSection cdata:
          AppendText(output, cdata.Value);
          return;
        case XmlElement element:
          AppendElement(output, element);
          return;
      }
    }

    private static void AppendText(StringBuilder output, string value) {
      var whitespacePending = false;
      output.Append("<noparse>");

      foreach (var character in WebUtility.HtmlDecode(value)) {
        if (char.IsWhiteSpace(character)) {
          whitespacePending = true;
          continue;
        }

        if (
          whitespacePending &&
          output.Length > 0 &&
          output[^1] != ' ' &&
          output[^1] != '\n'
        ) {
          output.Append(' ');
        }

        whitespacePending = false;
        output.Append(character);
      }

      if (
        whitespacePending &&
        output.Length > 0 &&
        output[^1] != ' ' &&
        output[^1] != '\n'
      ) {
        output.Append(' ');
      }

      output.Append("</noparse>");
    }

    private static void AppendElement(StringBuilder output, XmlElement element) {
      var name = element.LocalName;

      if (ParagraphElements.Contains(name)) {
        AppendParagraphBreak(output);
        AppendChildren(output, element);
        AppendParagraphBreak(output);
        return;
      }

      switch (name.ToLowerInvariant()) {
        case "a":
          AppendTag(output, element, "a", preserveAttributes: true);
          return;
        case "br":
          AppendLineBreak(output);
          return;
        case "b":
        case "i":
          AppendTag(output, element, name.ToLowerInvariant(), preserveAttributes: false);
          return;
        case "strong":
          AppendTag(output, element, "b", preserveAttributes: false);
          return;
        case "em":
          AppendTag(output, element, "i", preserveAttributes: false);
          return;
        case "h1":
          AppendParagraphBreak(output);
          output.Append("<style=\"h1\">");
          AppendChildren(output, element);
          output.Append("</style>");
          AppendParagraphBreak(output);
          return;
        default:
          AppendChildren(output, element);
          return;
      }
    }

    private static void AppendTag(
      StringBuilder output,
      XmlElement element,
      string outputName,
      bool preserveAttributes
    ) {
      output.Append('<').Append(outputName);

      if (preserveAttributes) {
        foreach (System.Xml.XmlAttribute attribute in element.Attributes) {
          output
            .Append(' ')
            .Append(attribute.Name)
            .Append("=\"")
            .Append(EscapeAttribute(attribute.Value))
            .Append('"');
        }
      }

      output.Append('>');
      AppendChildren(output, element);
      output.Append("</").Append(outputName).Append('>');
    }

    private static void AppendChildren(StringBuilder output, XmlElement element) {
      foreach (XmlNode child in element.ChildNodes) {
        AppendNode(output, child);
      }
    }

    private static void AppendParagraphBreak(StringBuilder output) {
      while (output.Length > 0 && (output[^1] == ' ' || output[^1] == '\t')) {
        output.Length--;
      }

      var trailingNewlines = 0;

      for (var index = output.Length - 1; index >= 0 && output[index] == '\n'; index--) {
        trailingNewlines++;
      }

      for (; trailingNewlines < 2; trailingNewlines++) {
        output.Append('\n');
      }
    }

    private static string Normalize(string value) {
      var output = new StringBuilder(value.Length);
      var newlineCount = 0;

      for (var index = 0; index < value.Length; index++) {
        var character = value[index];

        if (character == '\r') {
          if (index + 1 < value.Length && value[index + 1] == '\n') {
            index++;
          }

          character = '\n';
        }

        if (character == '\n') {
          while (output.Length > 0 && (output[^1] == ' ' || output[^1] == '\t')) {
            output.Length--;
          }

          if (newlineCount < 2) {
            output.Append('\n');
          }

          newlineCount++;
          continue;
        }

        if ((character == ' ' || character == '\t') && newlineCount > 0) {
          continue;
        }

        newlineCount = 0;
        output.Append(character);
      }

      return output.ToString().Trim();
    }

    private static string EscapeText(string value) {
      return value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
    }

    private static string EscapeAttribute(string value) {
      return EscapeText(value).Replace("\"", "&quot;");
    }
  }
}
