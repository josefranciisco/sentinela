using NetArchTest.Rules;
using FluentAssertions;

namespace Sentinela.Architecture.Tests;

public class ArchitectureTests
{
    private const string DomainNamespace = "Sentinela.Shared.Domain";
    private const string ApplicationNamespace = "Sentinela.Api";
    private const string InfrastructureNamespace = "Sentinela.Persistence";
    private const string SharedNamespace = "Sentinela.Shared";

    [Fact]
    public void Domain_Should_Not_Depend_On_Infrastructure()
    {
        var domainAssembly = typeof(Sentinela.Shared.Domain.Identity.User).Assembly;
        var infrastructureAssembly = typeof(Sentinela.Persistence.SentinelaDbContext).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn(infrastructureAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Domain_Should_Not_Depend_On_Application()
    {
        var domainAssembly = typeof(Sentinela.Shared.Domain.Identity.User).Assembly;
        var applicationAssembly = typeof(Sentinela.Api.Program).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOn(applicationAssembly.GetName().Name)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Entities_Should_Have_Private_Default_Constructors()
    {
        var domainAssembly = typeof(Sentinela.Shared.Domain.Identity.User).Assembly;
        var entityTypes = Types.InAssembly(domainAssembly)
            .That()
            .Inherit(typeof(Sentinela.Shared.Core.Entities.BaseEntity))
            .GetTypes();

        foreach (var type in entityTypes)
        {
            var constructors = type.GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            constructors.Should().ContainSingle(c => c.IsPrivate, 
                $"{type.Name} should have a private default constructor for EF Core");
        }
    }

    [Fact]
    public void Aggregates_Should_Only_Be_Modified_Through_Domain_Methods()
    {
        var domainAssembly = typeof(Sentinela.Shared.Domain.Identity.User).Assembly;
        var aggregateTypes = Types.InAssembly(domainAssembly)
            .That()
            .Inherit(typeof(Sentinela.Shared.Core.Entities.AggregateRoot))
            .GetTypes();

        foreach (var type in aggregateTypes)
        {
            var publicProperties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var prop in publicProperties)
            {
                if (prop.CanWrite && prop.SetMethod?.IsPublic == true)
                {
                    var allowed = new[] { "Id", "IsDeleted" };
                    if (!allowed.Contains(prop.Name))
                    {
                        true.Should().BeFalse($"{type.Name}.{prop.Name} should not have a public setter. Use domain methods instead.");
                    }
                }
            }
        }
    }

    [Fact]
    public void Controllers_Should_Be_Sealed()
    {
        var apiAssembly = typeof(Sentinela.Api.Controllers.v1.ComputersController).Assembly;
        var result = Types.InAssembly(apiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Services_Should_Be_Registered_As_Interfaces()
    {
        var apiAssembly = typeof(Sentinela.Api.Services.IAiAssistantService).Assembly;
        var types = Types.InAssembly(apiAssembly)
            .That()
            .HaveNameEndingWith("Service")
            .And()
            .AreInterfaces()
            .GetTypes();

        types.Should().NotBeEmpty("Services should implement interfaces for DI");
    }

    [Fact]
    public void Domain_Events_Should_Be_Immutable()
    {
        var domainAssembly = typeof(Sentinela.Shared.Core.Events.IDomainEvent).Assembly;
        var eventTypes = Types.InAssembly(domainAssembly)
            .That()
            .ImplementInterface(typeof(Sentinela.Shared.Core.Events.IDomainEvent))
            .GetTypes();

        foreach (var type in eventTypes)
        {
            var publicSetters = type.GetProperties()
                .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
                .Select(p => p.Name);

            publicSetters.Should().BeEmpty($"{type.Name} should be immutable (no public setters)");
        }
    }
}
