/* author: Copilot */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Reverse-compiles C# source into SMD files (architecture-schema.json), one per
// forward-compiled SMD in doc/architecture/bin, covering the types that SMD
// lists. Output goes to doc/architecture/reverse.
//
// usage: ReverseSmd [repository-root]

public static class Program {
  private static readonly HashSet<string> TypeModifiers = ["public", "internal", "protected", "private", "abstract", "sealed", "static", "partial", "readonly", "ref"];
  private static readonly HashSet<string> MemberModifiers = ["public", "internal", "protected", "private", "static", "readonly", "const", "abstract", "virtual", "override", "sealed", "async", "extern", "required"];

  private sealed record Declaration(BaseTypeDeclarationSyntax Syntax, SyntaxTree Tree, string Namespace, string? Outer);

  private static string _root = "";
  private static CSharpCompilation _compilation = null!;
  private static JsonArray _diagnostics = new();

  public static int Main(string[] args) {
    _root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
    var srcDir = Path.Combine(_root, "src", "trifecta");
    var forwardDir = Path.Combine(_root, "doc", "architecture", "bin");
    var outDir = Path.Combine(_root, "doc", "architecture", "reverse");

    var options = new CSharpParseOptions(LanguageVersion.Latest);
    var trees = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), options, f))
                    .ToList();
    var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                         .Split(Path.PathSeparator)
                         .Select(p => MetadataReference.CreateFromFile(p));
    _compilation = CSharpCompilation.Create("reverse", trees, references,
                                            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var declarations = new List<Declaration>();
    foreach (var tree in trees) Collect(tree, tree.GetRoot(), "", null, declarations);

    Directory.CreateDirectory(outDir);
    int missing = 0, total = 0;
    foreach (var forwardPath in Directory.GetFiles(forwardDir, "*.smd").OrderBy(f => f, StringComparer.Ordinal)) {
      var forward = JsonNode.Parse(File.ReadAllText(forwardPath))!.AsObject();
      _diagnostics = new();
      var types = new JsonArray();
      foreach (var wanted in forward["types"]!.AsArray()) {
        total++;
        var name = wanted!["name"]!.GetValue<string>();
        var ns = wanted["namespace"]?.GetValue<string>() ?? "";
        var documentedOuter = wanted["containing_type_id"] is null ? null : OuterName(wanted["containing_type_id"]!.GetValue<string>());
        var candidates = declarations.Where(d => d.Syntax.Identifier.Text == name && d.Namespace == ns).ToList();
        var pick = candidates.FirstOrDefault(d => d.Outer == documentedOuter) ?? candidates.FirstOrDefault();
        if (pick is null) {
          missing++;
          Diagnose("warning", "type-not-found", $"{ns}.{name} is documented but not declared in src/trifecta.", null);
          continue;
        }
        if (pick.Outer != documentedOuter)
          Diagnose("warning", "nesting-differs",
                   $"{ns}.{name} is documented {(documentedOuter is null ? "as a top-level type" : "inside " + documentedOuter)} but declared {(pick.Outer is null ? "at the top level" : "inside " + pick.Outer)}.",
                   pick);
        Diagnose("info", "type-source", $"{ns}.{name} read from {Relative(pick.Tree.FilePath)}.", pick);
        types.Add(BuildType(pick));
      }

      var smd = new JsonObject {
        ["author"] = "Copilot",
        ["format"] = "markdown-architecture-smd",
        ["version"] = 1,
        ["source"] = new JsonObject { ["kind"] = "csharp", ["path"] = "src/trifecta", ["language_version"] = "latest" },
        ["types"] = types,
      };
      if (_diagnostics.Count > 0) smd["diagnostics"] = _diagnostics;

      var json = JsonSerializer.Serialize(smd, new JsonSerializerOptions { WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
      File.WriteAllText(Path.Combine(outDir, Path.GetFileName(forwardPath)), json + "\n", new UTF8Encoding(false));
    }
    Console.WriteLine($"{total - missing} of {total} documented types reverse-compiled into {Relative(outDir)}.");
    return 0;
  }

  private static string OuterName(string id) {
    var s = id.Substring(id.LastIndexOf('.') + 1);
    var tick = s.IndexOf('`');
    return tick < 0 ? s : s.Substring(0, tick);
  }

  private static string Relative(string path) => Path.GetRelativePath(_root, path).Replace('\\', '/');

  private static void Collect(SyntaxTree tree, SyntaxNode node, string ns, string? outer, List<Declaration> into) {
    foreach (var child in node.ChildNodes()) {
      switch (child) {
        case BaseNamespaceDeclarationSyntax n:
          Collect(tree, n, ns.Length == 0 ? n.Name.ToString() : ns + "." + n.Name, outer, into);
          break;
        case ClassDeclarationSyntax or StructDeclarationSyntax or InterfaceDeclarationSyntax or EnumDeclarationSyntax:
          var t = (BaseTypeDeclarationSyntax)child;
          into.Add(new Declaration(t, tree, ns, outer));
          Collect(tree, t, ns, t.Identifier.Text, into);
          break;
      }
    }
  }

  private static void Diagnose(string severity, string code, string message, Declaration? at) {
    var d = new JsonObject { ["severity"] = severity, ["code"] = code, ["message"] = message };
    if (at is not null) d["source_location"] = Location(at.Syntax);
    _diagnostics.Add(d);
  }

  private static void DiagnoseNode(string severity, string code, string message, SyntaxNode node) {
    _diagnostics.Add(new JsonObject { ["severity"] = severity, ["code"] = code, ["message"] = message, ["source_location"] = Location(node) });
  }

  private static JsonObject Location(SyntaxNode node) {
    var span = node.SyntaxTree.GetLineSpan(node.Span);
    return new JsonObject {
      ["start_line"] = span.StartLinePosition.Line + 1,
      ["end_line"] = span.EndLinePosition.Line + 1,
      ["start_column"] = span.StartLinePosition.Character + 1,
      ["end_column"] = span.EndLinePosition.Character + 1,
    };
  }

  private static string Collapse(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

  private static string MetadataName(INamedTypeSymbol t) {
    var chain = new List<string>();
    for (INamedTypeSymbol? c = t; c is not null; c = c.ContainingType) chain.Insert(0, c.MetadataName);
    var ns = t.ContainingNamespace?.ToDisplayString();
    return (string.IsNullOrEmpty(ns) ? "" : ns + ".") + string.Join("+", chain);
  }

  private static JsonObject BuildType(Declaration d) {
    var syntax = d.Syntax;
    var model = _compilation.GetSemanticModel(d.Tree);
    var symbol = (INamedTypeSymbol)model.GetDeclaredSymbol(syntax)!;
    var kind = syntax switch {
      ClassDeclarationSyntax => "class",
      StructDeclarationSyntax => "struct",
      InterfaceDeclarationSyntax => "interface",
      _ => "enum",
    };

    var type = new JsonObject { ["id"] = "type:" + MetadataName(symbol), ["kind"] = kind, ["name"] = syntax.Identifier.Text };
    var typeParameters = (syntax as TypeDeclarationSyntax)?.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList();
    if (typeParameters is { Count: > 0 }) type["type_parameters"] = new JsonArray(typeParameters.Select(p => (JsonNode)p).ToArray());
    type["namespace"] = d.Namespace.Length == 0 ? null : d.Namespace;
    if (symbol.ContainingType is not null) type["containing_type_id"] = "type:" + MetadataName(symbol.ContainingType);
    type["modifiers"] = Modifiers(syntax.Modifiers, TypeModifiers, syntax);

    var bases = new JsonArray();
    if (syntax.BaseList is not null) {
      foreach (var b in syntax.BaseList.Types) {
        var r = TypeRef(b.Type, model);
        var resolved = model.GetSymbolInfo(b.Type).Symbol as INamedTypeSymbol;
        string relation;
        if (kind == "interface") relation = "extends";
        else if (resolved is not null && resolved.TypeKind != TypeKind.Error) relation = resolved.TypeKind == TypeKind.Interface ? "implements" : "extends";
        else {
          relation = IsInterfaceName(r["name"]!.GetValue<string>()) ? "implements" : "extends";
          DiagnoseNode("warning", "unresolved-base-type", $"Base type {b.Type} of {syntax.Identifier.Text} did not resolve; the relation was guessed from its name.", b);
        }
        var bt = new JsonObject { ["name"] = r["name"]!.GetValue<string>(), ["relation"] = relation };
        if (r["type_arguments"] is JsonArray args) bt["type_arguments"] = args.DeepClone();
        if (r["resolved_name"] is JsonNode rn) bt["resolved_name"] = rn.DeepClone();
        bases.Add(bt);
      }
    }
    type["base_types"] = bases;

    var members = new JsonArray();
    if (syntax is EnumDeclarationSyntax e) {
      foreach (var m in e.Members) {
        var em = new JsonObject { ["kind"] = "enum_member", ["name"] = m.Identifier.Text };
        if (m.EqualsValue is not null) em["value"] = Collapse(m.EqualsValue.Value.ToString());
        em["source_location"] = Location(m);
        members.Add(em);
      }
    } else {
      foreach (var m in ((TypeDeclarationSyntax)syntax).Members) BuildMember(m, syntax, model, members);
    }
    type["members"] = members;
    type["source_location"] = Location(syntax);
    return type;
  }

  private static bool IsInterfaceName(string name) {
    var simple = name.Substring(name.LastIndexOf('.') + 1);
    return simple.Length > 1 && simple[0] == 'I' && char.IsUpper(simple[1]);
  }

  private static JsonArray Modifiers(SyntaxTokenList tokens, HashSet<string> allowed, SyntaxNode at) {
    var result = new JsonArray();
    foreach (var token in tokens) {
      var text = token.Text;
      if (allowed.Contains(text)) result.Add(text);
      else DiagnoseNode("warning", "unsupported-modifier", $"The modifier '{text}' is not representable and was omitted.", at);
    }
    return result;
  }

  private static void BuildMember(MemberDeclarationSyntax m, BaseTypeDeclarationSyntax owner, SemanticModel model, JsonArray members) {
    switch (m) {
      case FieldDeclarationSyntax f:
        foreach (var v in f.Declaration.Variables) {
          var field = new JsonObject {
            ["kind"] = "field",
            ["name"] = v.Identifier.Text,
            ["type"] = TypeRef(f.Declaration.Type, model),
            ["modifiers"] = Modifiers(f.Modifiers, MemberModifiers, f),
          };
          if (v.Initializer is not null) field["initializer"] = Collapse(v.Initializer.Value.ToString());
          if (IsSingleton(f, v, owner)) field["annotations"] = new JsonArray("singleton");
          field["source_location"] = Location(v);
          field["source_text"] = Collapse(SourceBetween(f.Modifiers.Count > 0 ? f.Modifiers[0].SpanStart : f.Declaration.SpanStart, v.Identifier.Span.End, f.SyntaxTree));
          members.Add(field);
        }
        break;
      case PropertyDeclarationSyntax p:
        var accessors = new JsonArray();
        if (p.AccessorList is not null) {
          foreach (var a in p.AccessorList.Accessors) accessors.Add(a.Keyword.Text);
        } else {
          accessors.Add("get");
        }
        var prop = new JsonObject {
          ["kind"] = "property",
          ["name"] = p.Identifier.Text,
          ["type"] = TypeRef(p.Type, model),
          ["modifiers"] = Modifiers(p.Modifiers, MemberModifiers, p),
          ["accessors"] = accessors,
        };
        if (p.Initializer is not null) prop["initializer"] = Collapse(p.Initializer.Value.ToString());
        prop["source_location"] = Location(p);
        prop["source_text"] = Collapse(SourceBetween(p.Modifiers.Count > 0 ? p.Modifiers[0].SpanStart : p.Type.SpanStart, p.Identifier.Span.End, p.SyntaxTree));
        members.Add(prop);
        break;
      case ConstructorDeclarationSyntax c:
        members.Add(new JsonObject {
          ["kind"] = "constructor",
          ["name"] = c.Identifier.Text,
          ["modifiers"] = Modifiers(c.Modifiers, MemberModifiers, c),
          ["parameters"] = Parameters(c.ParameterList, model),
          ["source_location"] = Location(c),
          ["source_text"] = Collapse(SourceBetween(c.Modifiers.Count > 0 ? c.Modifiers[0].SpanStart : c.Identifier.SpanStart, c.ParameterList.Span.End, c.SyntaxTree)),
        });
        break;
      case MethodDeclarationSyntax md:
        var method = new JsonObject { ["kind"] = "method", ["name"] = md.Identifier.Text, ["return_type"] = TypeRef(md.ReturnType, model) };
        if (md.TypeParameterList is not null)
          method["type_parameters"] = new JsonArray(md.TypeParameterList.Parameters.Select(p => (JsonNode)p.Identifier.Text).ToArray());
        method["modifiers"] = Modifiers(md.Modifiers, MemberModifiers, md);
        method["parameters"] = Parameters(md.ParameterList, model);
        method["source_location"] = Location(md);
        method["source_text"] = Collapse(SourceBetween(md.Modifiers.Count > 0 ? md.Modifiers[0].SpanStart : md.ReturnType.SpanStart, md.ParameterList.Span.End, md.SyntaxTree));
        members.Add(method);
        break;
      case BaseTypeDeclarationSyntax:
        break; // nested types are documented in their own right
      default:
        DiagnoseNode("warning", "unsupported-member", $"{m.Kind()} in {owner.Identifier.Text} is not representable and was omitted.", m);
        break;
    }
  }

  private static string SourceBetween(int start, int end, SyntaxTree tree) => tree.GetText().ToString(new Microsoft.CodeAnalysis.Text.TextSpan(start, end - start));

  private static bool IsSingleton(FieldDeclarationSyntax f, VariableDeclaratorSyntax v, BaseTypeDeclarationSyntax owner) {
    if (owner is not ClassDeclarationSyntax || v.Identifier.Text != "singleton") return false;
    var mods = f.Modifiers.Select(x => x.Text).ToHashSet();
    if (!mods.Contains("public") || !mods.Contains("static")) return false;
    if (f.Declaration.Type.ToString() != owner.Identifier.Text) return false;
    return v.Initializer?.Value switch {
      ImplicitObjectCreationExpressionSyntax => true,
      ObjectCreationExpressionSyntax o => o.Type.ToString() == owner.Identifier.Text,
      _ => false,
    };
  }

  private static JsonArray Parameters(ParameterListSyntax list, SemanticModel model) {
    var result = new JsonArray();
    foreach (var p in list.Parameters) {
      var o = new JsonObject { ["name"] = p.Identifier.Text, ["type"] = p.Type is null ? new JsonObject { ["name"] = "var" } : TypeRef(p.Type, model) };
      var mods = new JsonArray();
      foreach (var t in p.Modifiers) {
        if (t.Text is "ref" or "out" or "in" or "params" or "this") mods.Add(t.Text);
        else DiagnoseNode("warning", "unsupported-modifier", $"The parameter modifier '{t.Text}' is not representable and was omitted.", p);
      }
      if (mods.Count > 0) o["modifiers"] = mods;
      if (p.Default is not null) o["default_value"] = Collapse(p.Default.Value.ToString());
      o["source_location"] = Location(p);
      result.Add(o);
    }
    return result;
  }

  private static JsonObject TypeRef(TypeSyntax ts, SemanticModel model) {
    switch (ts) {
      case NullableTypeSyntax n: {
        var inner = TypeRef(n.ElementType, model);
        inner["nullable"] = true;
        return Reorder(inner);
      }
      case ArrayTypeSyntax a: {
        var inner = TypeRef(a.ElementType, model);
        if (a.RankSpecifiers.Count > 1) DiagnoseNode("warning", "jagged-array", $"The jagged array type {a} was recorded with the rank of its first specifier only.", a);
        inner["array_rank"] = a.RankSpecifiers[0].Rank;
        return Reorder(inner);
      }
      case RefTypeSyntax r: return TypeRef(r.Type, model);
    }

    string name;
    JsonArray? args = null;
    switch (ts) {
      case GenericNameSyntax g:
        name = g.Identifier.Text;
        args = TypeArguments(g, model);
        break;
      case QualifiedNameSyntax q:
        if (q.Right is GenericNameSyntax qg) {
          name = q.Left + "." + qg.Identifier.Text;
          args = TypeArguments(qg, model);
        } else name = q.ToString();
        break;
      default:
        name = ts.ToString();
        break;
    }

    var o = new JsonObject { ["name"] = name };
    var symbol = model.GetSymbolInfo(ts).Symbol as INamedTypeSymbol;
    if (symbol is not null && symbol.TypeKind != TypeKind.Error && symbol.Locations.Any(l => l.IsInSource))
      o["resolved_name"] = MetadataName(symbol.OriginalDefinition);
    if (args is not null) o["type_arguments"] = args;
    return o;
  }

  private static JsonArray TypeArguments(GenericNameSyntax g, SemanticModel model) =>
      new(g.TypeArgumentList.Arguments.Select(a => (JsonNode)TypeRef(a, model)).ToArray());

  private static JsonObject Reorder(JsonObject o) {
    var order = new[] { "name", "resolved_name", "type_arguments", "array_rank", "nullable", "source_text" };
    var copy = new JsonObject();
    foreach (var k in order)
      if (o[k] is JsonNode v) copy[k] = v.DeepClone();
    return copy;
  }
}
