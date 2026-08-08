using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Xml;

namespace EffortStar.EditorXmlDocs.Editor {
  internal static class XmlRichTextConverter {
    static readonly HashSet<string> ParagraphElements = new(StringComparer.OrdinalIgnoreCase) {
      "description",
      "example",
      "exception",
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

    static void TrimBoundaryWhitespace(XmlElement root) {
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

    static void CollectTextNodes(XmlNode node, List<XmlNode> output) {
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

    static void AppendNode(StringBuilder output, XmlNode node) {
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

    static void AppendText(StringBuilder output, string value) {
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

    static void AppendElement(StringBuilder output, XmlElement element) {
      var name = element.LocalName;

      if (name.Equals("list", StringComparison.OrdinalIgnoreCase)) {
        AppendList(output, element);
        return;
      }

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
        case string n when ToHeading(n) is {} heading:
          AppendParagraphBreak(output);
          output.Append($"<b><size={HeadingSize(heading)}>");
          AppendChildren(output, element);
          output.AppendLine("</size></b>");
          AppendParagraphBreak(output);
          return;
        default:
          AppendChildren(output, element);
          return;
      }
    }

    static void AppendList(StringBuilder output, XmlElement list) {
      var numbered = list
        .GetAttribute("type")
        .Equals("number", StringComparison.OrdinalIgnoreCase);
      var itemNumber = 1;
      var hasItems = false;

      foreach (XmlNode child in list.ChildNodes) {
        if (
          child is not XmlElement item ||
          !item.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase)
        ) {
          continue;
        }

        if (!hasItems) {
          AppendParagraphBreak(output);
          hasItems = true;
        } else {
          AppendLineBreak(output);
        }

        AppendText(output, numbered ? $"{itemNumber}. " : "• ");
        AppendListItem(output, item);
        itemNumber++;
      }

      if (hasItems) {
        AppendParagraphBreak(output);
      }
    }

    static void AppendListItem(StringBuilder output, XmlElement item) {
      var term = GetChildElement(item, "term");
      var description = GetChildElement(item, "description");

      if (description == null) {
        if (term != null) {
          output.Append("<b>");
          AppendTrimmedChildren(output, term);
          output.Append("</b>");
        } else {
          AppendTrimmedChildren(output, item);
        }

        return;
      }

      if (term != null) {
        output.Append("<b>");
        AppendTrimmedChildren(output, term);
        output.Append("</b>");
        AppendText(output, " — ");
      }

      AppendTrimmedChildren(output, description);
    }

    static XmlElement? GetChildElement(XmlElement parent, string name) {
      foreach (XmlNode child in parent.ChildNodes) {
        if (
          child is XmlElement element &&
          element.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase)
        ) {
          return element;
        }
      }

      return null;
    }

    static void AppendTrimmedChildren(StringBuilder output, XmlElement element) {
      output.Append(Convert(element.InnerXml));
    }

    static string HeadingSize(int heading) => heading switch {
      6 => "1.1em",
      5 => "1.2em",
      4 => "1.3em",
      3 => "1.4em",
      2 => "1.5em",
      1 => "2em",
      _ => "1em"
    };

    static int? ToHeading(string name) =>
      name.Length == 2 && name[0] == 'h' && char.IsDigit(name[1])
        ? name[1] - '0'
        : null;

    static void AppendLineBreak(StringBuilder output) {
      while (output.Length > 0 && (output[^1] == ' ' || output[^1] == '\t')) {
        output.Length--;
      }

      output.Append('\n');
    }

    static void AppendTag(
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

    static void AppendChildren(StringBuilder output, XmlElement element) {
      foreach (XmlNode child in element.ChildNodes) {
        AppendNode(output, child);
      }
    }

    static void AppendParagraphBreak(StringBuilder output) {
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

    static string Normalize(string value) {
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

    static string EscapeText(string value) {
      return value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
    }

    static string EscapeAttribute(string value) {
      return EscapeText(value).Replace("\"", "&quot;");
    }
  }
}
