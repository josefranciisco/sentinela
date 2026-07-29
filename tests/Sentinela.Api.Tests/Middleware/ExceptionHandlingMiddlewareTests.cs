using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Http;
using Sentinela.Api.Middleware;
using Sentinela.Shared.Core.Exceptions;
using System.Text.Json;

namespace Sentinela.Api.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithDomainException_ReturnsBadRequest()
    {
        var middleware = new ExceptionHandlingMiddleware(next: _ => throw new DomainException("Test error", "TEST_001"));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        problem.GetProperty("title").GetString().Should().Be("Test error");
    }

    [Fact]
    public async Task InvokeAsync_WithNotFoundException_ReturnsNotFound()
    {
        var middleware = new ExceptionHandlingMiddleware(next: _ => throw new NotFoundException("Computer", Guid.NewGuid().ToString()));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_WithGenericException_ReturnsInternalServerError()
    {
        var middleware = new ExceptionHandlingMiddleware(next: _ => throw new Exception("Unexpected error"));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
    }
}
