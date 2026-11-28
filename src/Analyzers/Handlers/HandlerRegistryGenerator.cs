using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace YAF.Analyzers.Handlers;

/// <summary>
/// Emits, in a compilation that declares a request or has a handler or validator, the assembly's <c>[assembly: YafHandlers(…)]</c> and
/// the file-local <c>YAF.Generated.YafHandlerRegistry</c> it names (04 §2, §5): the declared and the handled request types, the
/// registration of every request handler, stream query handler, domain event handler and validator as a scoped service, and a switch on
/// the runtime type of a request, stream query or domain event to the <c>IRequestRunner</c> call with its static types. A type the
/// registry cannot name (private, or inside a generic type), a generic or abstract type, and a handler or validator of a request, result,
/// item or event type the registry cannot name are left out. Being file-local, the registry is reached only through the attribute and is
/// invisible to every other assembly, so that a friend assembly with its own handlers does not clash with it.
/// </summary>
[Generator(LanguageNames.CSharp)]
internal sealed class HandlerRegistryGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat s_typeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var entries = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { BaseList: not null },
                static (syntax, token) => ToEntries(syntax, token))
            .SelectMany(static (entries, _) => entries)
            .Collect();

        context.RegisterSourceOutput(entries, static (output, entries) => Emit(output, entries));
    }

    private static ImmutableArray<Entry> ToEntries(GeneratorSyntaxContext syntax, CancellationToken token)
    {
        var compilation = syntax.SemanticModel.Compilation;
        if (syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, token) is not INamedTypeSymbol { IsAbstract: false } type
            || type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || IsInGenericType(type)
            || !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
        {
            return [];
        }

        var name = type.ToDisplayString(s_typeFormat);
        var entries = ImmutableArray.CreateBuilder<Entry>();
        foreach (var contract in type.AllInterfaces)
        {
            var arguments = contract.TypeArguments.Select(argument => argument.ToDisplayString(s_typeFormat)).ToArray();
            var depth = Depth(contract.TypeArguments.FirstOrDefault());

            // The registry names a handler's type arguments in typeof, case and AddScoped; a declared request names only itself.
            var named = contract.TypeArguments.All(argument => compilation.IsSymbolAccessibleWithin(argument, compilation.Assembly));
            switch (Contract(contract))
            {
                case "YAF.Application.IRequest`1" or "YAF.Application.IStreamQuery`1":
                    entries.Add(new Entry(Kind.Declared, name, name, null, 0));
                    break;
                case "YAF.Application.IRequestHandler`2" when named:
                    entries.Add(new Entry(Kind.Request, name, arguments[0], arguments[1], depth));
                    break;
                case "YAF.Application.IStreamQueryHandler`2" when named:
                    entries.Add(new Entry(Kind.Stream, name, arguments[0], arguments[1], depth));
                    break;
                case "YAF.Application.IDomainEventHandler`1" when named:
                    entries.Add(new Entry(Kind.Event, name, arguments[0], null, depth));
                    break;
                case "YAF.Application.Validation.IValidator`1" when named:
                    entries.Add(new Entry(Kind.Validator, name, arguments[0], null, 0));
                    break;
            }
        }

        return entries.ToImmutable();
    }

    private static string Contract(INamedTypeSymbol contract) => contract.ContainingNamespace.ToDisplayString() + "." + contract.MetadataName;

    private static bool IsInGenericType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }

    // How many base types a request or event has, so that the switch tests a derived type before its base (no case subsumes a later one).
    private static int Depth(ITypeSymbol? type)
    {
        var depth = 0;
        for (var current = type?.BaseType; current is not null; current = current.BaseType)
        {
            depth++;
        }

        return depth;
    }

    private static void Emit(SourceProductionContext output, ImmutableArray<Entry> found)
    {
        var entries = found.Distinct().ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var register = new StringBuilder();
        foreach (var entry in entries.Where(entry => entry.Kind != Kind.Declared))
        {
            var service = entry.Kind switch
            {
                Kind.Request => $"global::YAF.Application.IRequestHandler<{entry.Target}, {entry.Result}>",
                Kind.Stream => $"global::YAF.Application.IStreamQueryHandler<{entry.Target}, {entry.Result}>",
                Kind.Event => $"global::YAF.Application.IDomainEventHandler<{entry.Target}>",
                _ => $"global::YAF.Application.Validation.IValidator<{entry.Target}>",
            };
            register.Append("            global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped<")
                .Append(service).Append(", ").Append(entry.Type).AppendLine(">(services);");
        }

        output.AddSource("YAF.HandlerRegistry.g.cs", $$"""
            // <auto-generated/>
            #nullable enable
            [assembly: global::YAF.Application.Registration.YafHandlers(typeof(global::YAF.Generated.YafHandlerRegistry))]

            namespace YAF.Generated
            {
                file sealed class YafHandlerRegistry : global::YAF.Application.Registration.IHandlerRegistry
                {
                    public global::System.Collections.Generic.IReadOnlyCollection<global::System.Type> DeclaredRequests { get; } =
                        new global::System.Type[] { {{TypeList(entries, Kind.Declared)}} };

                    public global::System.Collections.Generic.IReadOnlyCollection<global::System.Type> HandledRequests { get; } =
                        new global::System.Type[] { {{TypeList(entries, Kind.Request, Kind.Stream)}} };

                    public void Register(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                    {
            {{register}}        }

                    public bool TrySend<TResult>(global::YAF.Application.IRequest<TResult> request, global::YAF.Application.Registration.IRequestRunner runner, global::System.Threading.CancellationToken ct, out global::System.Threading.Tasks.ValueTask<TResult> result)
                    {
                        switch (request)
                        {
            {{Cases(entries, Kind.Request, entry => $"result = (global::System.Threading.Tasks.ValueTask<TResult>)(object)runner.SendAsync<{entry.Target}, {entry.Result}>(r, ct);")}}                default:
                                result = default;
                                return false;
                        }
                    }

                    public bool TryStream<TItem>(global::YAF.Application.IStreamQuery<TItem> query, global::YAF.Application.Registration.IRequestRunner runner, global::System.Threading.CancellationToken ct, out global::System.Collections.Generic.IAsyncEnumerable<TItem> items)
                    {
                        switch (query)
                        {
            {{Cases(entries, Kind.Stream, entry => $"items = (global::System.Collections.Generic.IAsyncEnumerable<TItem>)runner.StreamAsync<{entry.Target}, {entry.Result}>(r, ct);")}}                default:
                                items = global::System.Linq.AsyncEnumerable.Empty<TItem>();
                                return false;
                        }
                    }

                    public bool TryPublish(global::YAF.Domain.Events.IDomainEvent domainEvent, global::YAF.Application.Registration.IRequestRunner runner, global::System.Threading.CancellationToken ct, out global::System.Threading.Tasks.ValueTask<global::YAF.Core.Results.Result> result)
                    {
                        switch (domainEvent)
                        {
            {{Cases(entries, Kind.Event, entry => $"result = runner.PublishAsync<{entry.Target}>(r, ct);")}}                default:
                                result = default;
                                return false;
                        }
                    }
                }
            }

            """);
    }

    private static string TypeList(List<Entry> entries, params Kind[] kinds) =>
        string.Join(", ", entries.Where(entry => kinds.Contains(entry.Kind)).Select(entry => $"typeof({entry.Target})").Distinct());

    // One case per target type, a derived type before its base; a request type handled twice (YAF4005) gets its first handler's case, and
    // an event type one case, in which the runner runs all its handlers.
    private static string Cases(List<Entry> entries, Kind kind, Func<Entry, string> call)
    {
        var cases = new StringBuilder();
        foreach (var entry in entries.Where(entry => entry.Kind == kind).GroupBy(entry => entry.Target).Select(group => group.First())
            .OrderByDescending(entry => entry.Depth))
        {
            cases.Append("                case ").Append(entry.Target).AppendLine(" r:")
                .Append("                    ").AppendLine(call(entry))
                .AppendLine("                    return true;");
        }

        return cases.ToString();
    }

    private enum Kind
    {
        Declared,
        Request,
        Stream,
        Event,
        Validator,
    }

    private sealed record Entry(Kind Kind, string Type, string Target, string? Result, int Depth);
}
