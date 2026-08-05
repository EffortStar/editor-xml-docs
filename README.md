# Editor XML Docs

> Display C# XML documentation in the Unity Inspector.

Editor XML Docs adds inspector documentation without duplicating comment text in Unity attributes. It supports UI Toolkit and IMGUI inspectors.

## Install

Add the package from its Git URL in Unity Package Manager:

```text
https://github.com/EffortStar/editor-xml-docs.git
```

The package requires Unity 2022.2.2f1 or later.

## Usage

Add one of the following attributes to a serialized field:

- `[InlineDoc]` displays its XML documentation above the field.
- `[TooltipDoc]` displays its XML documentation as the field's tooltip.

```csharp
using EffortStar.EditorXmlDocs;
using UnityEngine;

public sealed class Example : MonoBehaviour {
  /// <summary>How quickly the character moves.</summary>
  [InlineDoc]
  public float Speed;

  /// <summary>The maximum number of targets.</summary>
  [TooltipDoc]
  public int TargetLimit;
}
```

The Inspector offers to configure the owning assembly's `csc.rsp` when its XML documentation file is missing.

## Maintainers

[EffortStar](https://github.com/EffortStar)

## Contributing

Issues and pull requests are welcome in the [GitHub repository](https://github.com/EffortStar/editor-xml-docs).

## License

[MIT](LICENSE) © 2026 EffortStar
