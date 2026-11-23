using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Writes the public surface of a C# source directory as a stable, sorted,
// C#-shaped text file:
//
//   ApiDump <source-dir> <output-file>
//
// It reads source rather than a built assembly, so it runs without Unity. The
// Unity engine types stay unresolved, which is fine: a signature that names one
// still prints it by name. Members are sorted, so moving code around does not
// change the dump; only a change to the public surface does.
internal static class Program
{
    // Unity compiles the runtime assembly once per target. The public surface
    // must be the same for each, otherwise a change could hide behind a symbol
    // the dump never sets.
    private static readonly string[][] Configurations =
    {
        new string[0],
        new[] { "UNITY_EDITOR" },
        new[] { "UNITY_ANDROID" },
        new[] { "UNITY_IOS" },
    };

    private static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                              SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                              SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat DeclarationNameFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                         SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly SymbolDisplayFormat ParameterFormat = TypeFormat.WithParameterOptions(
        SymbolDisplayParameterOptions.IncludeType |
        SymbolDisplayParameterOptions.IncludeName |
        SymbolDisplayParameterOptions.IncludeDefaultValue |
        SymbolDisplayParameterOptions.IncludeParamsRefOut);

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: ApiDump <source-dir> <output-file>");
            return 2;
        }

        var sourceDirectory = Path.GetFullPath(args[0]);
        var outputPath = Path.GetFullPath(args[1]);
        var sources = Directory
            .GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToArray();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();

        string dump = null;
        string dumpConfiguration = null;
        foreach (var symbols in Configurations)
        {
            var configuration = symbols.Length == 0 ? "no platform symbols" : string.Join(", ", symbols);
            // Unity 6 compiles C# 9.
            var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: symbols);
            var trees = sources
                .Select(source => CSharpSyntaxTree.ParseText(source.Text, parseOptions, source.Path))
                .ToArray();

            var syntaxErrors = trees
                .SelectMany(tree => tree.GetDiagnostics())
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToArray();
            if (syntaxErrors.Length > 0)
            {
                foreach (var error in syntaxErrors)
                {
                    Console.Error.WriteLine($"{error} ({configuration})");
                }

                return 1;
            }

            var compilation = CSharpCompilation.Create(
                "Appstack.Unity",
                trees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var current = Render(compilation.Assembly.GlobalNamespace);

            if (dump == null)
            {
                dump = current;
                dumpConfiguration = configuration;
            }
            else if (dump != current)
            {
                Console.Error.WriteLine(
                    $"error: the public API differs between '{dumpConfiguration}' and '{configuration}'. " +
                    "Keep platform-specific code internal and give every platform the same public surface.");
                return 1;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, dump, new UTF8Encoding(false));
        return 0;
    }

    private static string Render(INamespaceSymbol globalNamespace)
    {
        var writer = new Writer();
        writer.Line("// Public API of the Appstack.Unity runtime assembly (Runtime/).");
        writer.Line("// Generated by scripts~/api-baseline.sh. Do not edit by hand.");

        var namespaces = AllNamespaces(globalNamespace)
            .Select(ns => (Namespace: ns, Types: ns.GetTypeMembers().Where(IsVisible).OrderBy(t => t.MetadataName, StringComparer.Ordinal).ToArray()))
            .Where(entry => entry.Types.Length > 0)
            .OrderBy(entry => entry.Namespace.ToDisplayString(), StringComparer.Ordinal);

        foreach (var (ns, types) in namespaces)
        {
            writer.Line();
            if (ns.IsGlobalNamespace)
            {
                WriteTypes(writer, types);
                continue;
            }

            writer.Line("namespace " + ns.ToDisplayString());
            writer.Line("{");
            writer.Indent++;
            WriteTypes(writer, types);
            writer.Indent--;
            writer.Line("}");
        }

        return writer.ToString();
    }

    private static IEnumerable<INamespaceSymbol> AllNamespaces(INamespaceSymbol ns)
    {
        yield return ns;
        foreach (var child in ns.GetNamespaceMembers().SelectMany(AllNamespaces))
        {
            yield return child;
        }
    }

    private static void WriteTypes(Writer writer, IReadOnlyList<INamedTypeSymbol> types)
    {
        for (var i = 0; i < types.Count; i++)
        {
            if (i > 0)
            {
                writer.Line();
            }

            WriteType(writer, types[i]);
        }
    }

    private static void WriteType(Writer writer, INamedTypeSymbol type)
    {
        WriteAttributes(writer, type);

        if (type.TypeKind == TypeKind.Delegate)
        {
            var invoke = type.DelegateInvokeMethod;
            writer.Line(
                $"{AccessibilityKeyword(type)} delegate {ReturnType(invoke)} {type.ToDisplayString(DeclarationNameFormat)}" +
                $"({Parameters(invoke.Parameters)}){Constraints(type.TypeParameters)};");
            return;
        }

        var header = new StringBuilder(AccessibilityKeyword(type));
        header.Append(TypeModifiers(type));
        header.Append(' ').Append(TypeKeyword(type));
        header.Append(' ').Append(type.ToDisplayString(DeclarationNameFormat));

        var bases = new List<string>();
        if (type.TypeKind == TypeKind.Enum)
        {
            if (type.EnumUnderlyingType.SpecialType != SpecialType.System_Int32)
            {
                bases.Add(type.EnumUnderlyingType.ToDisplayString(TypeFormat));
            }
        }
        else
        {
            if (type.TypeKind == TypeKind.Class &&
                type.BaseType != null &&
                type.BaseType.SpecialType != SpecialType.System_Object)
            {
                bases.Add(type.BaseType.ToDisplayString(TypeFormat));
            }

            bases.AddRange(type.Interfaces
                .Where(iface => iface.TypeKind == TypeKind.Error || IsVisible(iface))
                .Select(iface => iface.ToDisplayString(TypeFormat))
                .OrderBy(name => name, StringComparer.Ordinal));
        }

        if (bases.Count > 0)
        {
            header.Append(" : ").Append(string.Join(", ", bases));
        }

        header.Append(Constraints(type.TypeParameters));
        writer.Line(header.ToString());
        writer.Line("{");
        writer.Indent++;

        if (type.TypeKind == TypeKind.Enum)
        {
            foreach (var field in type.GetMembers().OfType<IFieldSymbol>()
                         .Where(field => field.HasConstantValue)
                         .OrderBy(field => Convert.ToDecimal(field.ConstantValue))
                         .ThenBy(field => field.Name, StringComparer.Ordinal))
            {
                WriteAttributes(writer, field);
                writer.Line($"{field.Name} = {Literal(field.ConstantValue)},");
            }
        }
        else
        {
            WriteMembers(writer, type);
        }

        writer.Indent--;
        writer.Line("}");
    }

    private static void WriteMembers(Writer writer, INamedTypeSymbol type)
    {
        var groups = new[]
        {
            type.GetMembers().OfType<IFieldSymbol>().Where(f => IsVisible(f, type))
                .Select(f => (Symbol: (ISymbol)f, Text: Field(f))),
            type.InstanceConstructors.Where(c => IsVisible(c, type) && !IsImplicitStructConstructor(c))
                .Select(c => (Symbol: (ISymbol)c, Text: Constructor(c, type))),
            type.GetMembers().OfType<IPropertySymbol>().Where(p => IsVisible(p, type) && p.ExplicitInterfaceImplementations.IsEmpty)
                .Select(p => (Symbol: (ISymbol)p, Text: Property(p, type))),
            type.GetMembers().OfType<IEventSymbol>().Where(e => IsVisible(e, type) && e.ExplicitInterfaceImplementations.IsEmpty)
                .Select(e => (Symbol: (ISymbol)e, Text: Event(e, type))),
            type.GetMembers().OfType<IMethodSymbol>().Where(m => IsVisible(m, type) && IsListedMethod(m))
                .Select(m => (Symbol: (ISymbol)m, Text: Method(m, type))),
        };

        var first = true;
        foreach (var group in groups)
        {
            var members = group
                .OrderBy(member => member.Symbol.Name, StringComparer.Ordinal)
                .ThenBy(member => member.Text, StringComparer.Ordinal)
                .ToArray();
            if (members.Length == 0)
            {
                continue;
            }

            if (!first)
            {
                writer.Line();
            }

            first = false;
            foreach (var member in members)
            {
                WriteAttributes(writer, member.Symbol);
                writer.Line(member.Text);
            }
        }

        var nestedTypes = type.GetTypeMembers()
            .Where(IsVisible)
            .OrderBy(nested => nested.MetadataName, StringComparer.Ordinal)
            .ToArray();
        if (nestedTypes.Length > 0)
        {
            if (!first)
            {
                writer.Line();
            }

            WriteTypes(writer, nestedTypes);
        }
    }

    private static string Field(IFieldSymbol field)
    {
        var modifiers = field.IsConst ? " const" :
            (field.IsStatic ? " static" : "") + (field.IsReadOnly ? " readonly" : "") + (field.IsVolatile ? " volatile" : "");
        var text = $"{AccessibilityKeyword(field)}{modifiers} {field.Type.ToDisplayString(TypeFormat)} {field.Name}";
        return field.IsConst ? $"{text} = {Literal(field.ConstantValue)};" : text + ";";
    }

    private static string Constructor(IMethodSymbol constructor, INamedTypeSymbol type)
    {
        return $"{AccessibilityKeyword(constructor)} {type.Name}({Parameters(constructor.Parameters)});";
    }

    private static string Property(IPropertySymbol property, INamedTypeSymbol type)
    {
        var name = property.IsIndexer ? $"this[{Parameters(property.Parameters)}]" : property.Name;
        var accessors = new List<string>();
        if (property.GetMethod != null && IsVisible(property.GetMethod, type))
        {
            accessors.Add(Accessor(property.GetMethod, property, "get"));
        }

        if (property.SetMethod != null && IsVisible(property.SetMethod, type))
        {
            accessors.Add(Accessor(property.SetMethod, property, property.SetMethod.IsInitOnly ? "init" : "set"));
        }

        return $"{MemberPrefix(property, type)}{RefPrefix(property.RefKind)}{property.Type.ToDisplayString(TypeFormat)} {name} " +
               $"{{ {string.Join(" ", accessors)} }}";
    }

    private static string Accessor(IMethodSymbol accessor, IPropertySymbol property, string keyword)
    {
        return accessor.DeclaredAccessibility == property.DeclaredAccessibility
            ? keyword + ";"
            : $"{AccessibilityKeyword(accessor)} {keyword};";
    }

    private static string Event(IEventSymbol @event, INamedTypeSymbol type)
    {
        return $"{MemberPrefix(@event, type)}event {@event.Type.ToDisplayString(TypeFormat)} {@event.Name};";
    }

    private static string Method(IMethodSymbol method, INamedTypeSymbol type)
    {
        var parameters = Parameters(method.Parameters);
        switch (method.MethodKind)
        {
            case MethodKind.UserDefinedOperator:
                var token = SyntaxFacts.GetText(SyntaxFacts.GetOperatorKind(method.MetadataName));
                return $"{MemberPrefix(method, type)}{ReturnType(method)} operator {token}({parameters});";
            case MethodKind.Conversion:
                var direction = method.Name == WellKnownMemberNames.ImplicitConversionName ? "implicit" : "explicit";
                return $"{MemberPrefix(method, type)}{direction} operator {ReturnType(method)}({parameters});";
            default:
                var name = method.ToDisplayString(DeclarationNameFormat);
                if (method.IsExtensionMethod)
                {
                    // A parameter displayed on its own never shows `this`.
                    parameters = "this " + parameters;
                }

                return $"{MemberPrefix(method, type)}{ReturnType(method)} {name}({parameters})" +
                       $"{Constraints(method.TypeParameters)};";
        }
    }

    private static bool IsListedMethod(IMethodSymbol method)
    {
        return method.ExplicitInterfaceImplementations.IsEmpty &&
               (method.MethodKind == MethodKind.Ordinary ||
                method.MethodKind == MethodKind.UserDefinedOperator ||
                method.MethodKind == MethodKind.Conversion);
    }

    private static bool IsImplicitStructConstructor(IMethodSymbol constructor)
    {
        return constructor.IsImplicitlyDeclared &&
               constructor.ContainingType.TypeKind == TypeKind.Struct &&
               constructor.Parameters.IsEmpty;
    }

    // Members a consumer can see: public ones, and protected ones on a type
    // they can derive from.
    private static bool IsVisible(ISymbol member, INamedTypeSymbol type)
    {
        if (member.IsImplicitlyDeclared && !(member is IMethodSymbol { MethodKind: MethodKind.Constructor }))
        {
            return false;
        }

        switch (member.DeclaredAccessibility)
        {
            case Microsoft.CodeAnalysis.Accessibility.Public:
                return true;
            case Microsoft.CodeAnalysis.Accessibility.Protected:
            case Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal:
                return !type.IsSealed && !type.IsStatic;
            default:
                return false;
        }
    }

    private static bool IsVisible(INamedTypeSymbol type)
    {
        return type.ContainingType == null
            ? type.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public
            : IsVisible(type.ContainingType) && IsVisible(type, type.ContainingType);
    }

    private static string MemberPrefix(ISymbol member, INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            return member.IsStatic ? "static " : "";
        }

        var prefix = new StringBuilder(AccessibilityKeyword(member));
        if (member.IsStatic) prefix.Append(" static");
        if (member.IsAbstract) prefix.Append(" abstract");
        if (member.IsVirtual) prefix.Append(" virtual");
        if (member.IsOverride) prefix.Append(member.IsSealed ? " sealed override" : " override");
        if (member is IMethodSymbol { IsReadOnly: true } && type.TypeKind == TypeKind.Struct && !type.IsReadOnly)
        {
            prefix.Append(" readonly");
        }

        return prefix.Append(' ').ToString();
    }

    private static string TypeModifiers(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Class)
        {
            if (type.IsStatic) return " static";
            if (type.IsAbstract) return " abstract";
            if (type.IsSealed) return " sealed";
        }

        if (type.TypeKind == TypeKind.Struct)
        {
            return (type.IsReadOnly ? " readonly" : "") + (type.IsRefLikeType ? " ref" : "");
        }

        return "";
    }

    private static string TypeKeyword(INamedTypeSymbol type)
    {
        switch (type.TypeKind)
        {
            case TypeKind.Class:
                return type.IsRecord ? "record" : "class";
            case TypeKind.Struct:
                return type.IsRecord ? "record struct" : "struct";
            case TypeKind.Interface:
                return "interface";
            case TypeKind.Enum:
                return "enum";
            default:
                throw new NotSupportedException($"Unexpected type kind {type.TypeKind} for {type}.");
        }
    }

    private static string AccessibilityKeyword(ISymbol symbol)
    {
        switch (symbol.DeclaredAccessibility)
        {
            case Microsoft.CodeAnalysis.Accessibility.Public:
                return "public";
            case Microsoft.CodeAnalysis.Accessibility.Protected:
                return "protected";
            case Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal:
                // Outside the assembly this is plain protected.
                return "protected";
            default:
                return symbol.DeclaredAccessibility.ToString().ToLowerInvariant();
        }
    }

    private static string ReturnType(IMethodSymbol method)
    {
        return RefPrefix(method.RefKind) + method.ReturnType.ToDisplayString(TypeFormat);
    }

    private static string RefPrefix(RefKind refKind)
    {
        switch (refKind)
        {
            case RefKind.Ref:
                return "ref ";
            case RefKind.RefReadOnly:
                return "ref readonly ";
            default:
                return "";
        }
    }

    private static string Parameters(ImmutableArray<IParameterSymbol> parameters)
    {
        return string.Join(", ", parameters.Select(parameter => parameter.ToDisplayString(ParameterFormat)));
    }

    private static string Constraints(ImmutableArray<ITypeParameterSymbol> typeParameters)
    {
        var builder = new StringBuilder();
        foreach (var parameter in typeParameters)
        {
            var constraints = new List<string>();
            if (parameter.HasReferenceTypeConstraint) constraints.Add("class");
            if (parameter.HasUnmanagedTypeConstraint) constraints.Add("unmanaged");
            else if (parameter.HasValueTypeConstraint) constraints.Add("struct");
            if (parameter.HasNotNullConstraint) constraints.Add("notnull");
            constraints.AddRange(parameter.ConstraintTypes.Select(t => t.ToDisplayString(TypeFormat)));
            if (parameter.HasConstructorConstraint) constraints.Add("new()");

            if (constraints.Count > 0)
            {
                builder.Append($" where {parameter.Name} : {string.Join(", ", constraints)}");
            }
        }

        return builder.ToString();
    }

    // [Obsolete] and [Flags] change what callers may do, so they are part of
    // the surface. Other attributes are not listed.
    private static void WriteAttributes(Writer writer, ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString(TypeFormat);
            if (name != "System.ObsoleteAttribute" && name != "System.FlagsAttribute")
            {
                continue;
            }

            var arguments = attribute.ConstructorArguments.Select(argument => Literal(argument.Value)).ToArray();
            var shortName = name.Substring(0, name.Length - "Attribute".Length);
            writer.Line(arguments.Length == 0 ? $"[{shortName}]" : $"[{shortName}({string.Join(", ", arguments)})]");
        }
    }

    private static string Literal(object value)
    {
        return value == null ? "null" : SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false);
    }

    private sealed class Writer
    {
        private readonly StringBuilder builder = new StringBuilder();

        public int Indent { get; set; }

        public void Line(string text = "")
        {
            if (text.Length > 0)
            {
                builder.Append(' ', Indent * 4).Append(text);
            }

            builder.Append('\n');
        }

        public override string ToString()
        {
            return builder.ToString();
        }
    }
}
