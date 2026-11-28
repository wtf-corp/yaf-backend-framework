using System.Reflection;
using Application.Tests.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using YAF.Application;
using YAF.Application.Errors;
using YAF.Application.UnitOfWork;
using YAF.Core.Results;

namespace Application.Tests.Errors;

public sealed class ErrorCatalogProviderTests
{
    [Fact]
    public void Errors_ListsTheServicesAndCoreCodes()
    {
        string[] expected = [Listing.Errors.NotFound.Code, ApplicationErrors.ConcurrencyConflict.Code, CoreErrors.Uninitialised.Code];
        using var provider = Build(new ServiceCollection().AddYafApplication(_ => { }));

        var errors = provider.GetRequiredService<IErrorCatalogProvider>().Errors;

        errors.Select(entry => entry.Descriptor.Code).Should().Contain(expected);
    }

    [Fact]
    public void Errors_KeepDeclaringTypes()
    {
        using var provider = Build(new ServiceCollection().AddYafApplication(_ => { }));

        var errors = provider.GetRequiredService<IErrorCatalogProvider>().Errors;

        errors.Should().Contain(new ErrorCatalogEntry(Listing.Errors.NotFound, "Application.Tests.Errors.Listing+Errors"))
            .And.Contain(new ErrorCatalogEntry(ApplicationErrors.ConcurrencyConflict, "YAF.Application.UnitOfWork.ApplicationErrors"));
    }

    [Fact]
    public void Errors_AssemblyWithoutAttribute_IsSkipped()
    {
        using var provider = Build(WithSource(typeof(FactAttribute).Assembly));

        var errors = provider.GetRequiredService<IErrorCatalogProvider>().Errors;

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Errors_ReadsOnlyTheSourcesAssemblies()
    {
        using var provider = Build(WithSource(typeof(ApplicationErrors).Assembly));

        var errors = provider.GetRequiredService<IErrorCatalogProvider>().Errors;

        errors.Should().Equal(
            new ErrorCatalogEntry(ApplicationErrors.ConcurrencyConflict, "YAF.Application.UnitOfWork.ApplicationErrors"),
            new ErrorCatalogEntry(ApplicationErrors.DependencyUnavailable, "YAF.Application.UnitOfWork.ApplicationErrors"),
            new ErrorCatalogEntry(ApplicationErrors.DomainEventDepthExceeded, "YAF.Application.UnitOfWork.ApplicationErrors"));
    }

    [Fact]
    public void Errors_AssemblyOfARegisteredHandlerRegistry_IsRead()
    {
        var registry = new HandBuiltRegistry();
        var services = new ServiceCollection().AddYafApplication(app => app.AddHandlers(registry.GetType().Assembly, registry));
        using var provider = Build(services.Replace(ServiceDescriptor.Singleton<IErrorCatalogAssemblySource>(new FixedAssemblySource())));

        var errors = provider.GetRequiredService<IErrorCatalogProvider>().Errors;

        errors.Should().Contain(new ErrorCatalogEntry(Listing.Errors.NotFound, "Application.Tests.Errors.Listing+Errors"));
    }

    private static ServiceProvider Build(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    private static IServiceCollection WithSource(params Assembly[] assemblies) =>
        new ServiceCollection().AddYafApplication(_ => { })
            .Replace(ServiceDescriptor.Singleton<IErrorCatalogAssemblySource>(new FixedAssemblySource(assemblies)));
}
