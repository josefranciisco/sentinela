using Xunit;
using FluentAssertions;
using Moq;
using Sentinela.Identity.Services;
using Sentinela.Shared.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Sentinela.Identity.Models;

namespace Sentinela.Identity.Tests;

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILdapService> _ldapServiceMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _tokenServiceMock = new Mock<ITokenService>();
        _ldapServiceMock = new Mock<ILdapService>();
        _cacheServiceMock = new Mock<ICacheService>();
        _eventBusMock = new Mock<IEventBus>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        _authService = new AuthService(
            _userManagerMock.Object,
            _tokenServiceMock.Object,
            _ldapServiceMock.Object,
            _cacheServiceMock.Object,
            _eventBusMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsLoginResponse()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "testuser",
            Email = "test@test.com",
            IsActive = true,
            TwoFactorEnabled = false
        };
        var roles = new List<string> { "User" };
        var token = "access_token_123";
        var refreshToken = "refresh_token_123";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        _userManagerMock.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "password")).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(roles);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user, roles)).Returns((token, expiresAt));
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(refreshToken);

        var request = new LoginRequest("testuser", "test@test.com", "password", null, null);

        var result = await _authService.LoginAsync(request, "device-info", "127.0.0.1");

        result.Should().NotBeNull();
        result.AccessToken.Should().Be(token);
        result.RefreshToken.Should().Be(refreshToken);
        result.User.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task LoginAsync_WithInvalidCredentials_ThrowsException()
    {
        _userManagerMock.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync((ApplicationUser)null);

        var request = new LoginRequest("testuser", "test@test.com", "wrong_password", null, null);

        await FluentActions
            .Awaiting(() => _authService.LoginAsync(request, null!, null!))
            .Should()
            .ThrowAsync<Exception>()
            .WithMessage("Invalid credentials");
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ThrowsException()
    {
        var user = new ApplicationUser { UserName = "inactive", IsActive = false };
        _userManagerMock.Setup(x => x.FindByNameAsync("inactive")).ReturnsAsync(user);

        var request = new LoginRequest("inactive", "inactive@test.com", "password", null, null);

        await FluentActions
            .Awaiting(() => _authService.LoginAsync(request, null!, null!))
            .Should()
            .ThrowAsync<Exception>()
            .WithMessage("User account is inactive or locked");
    }

    [Fact]
    public async Task RegisterAsync_WithNewUser_CreatesSuccessfully()
    {
        _userManagerMock.Setup(x => x.FindByEmailAsync("new@test.com")).ReturnsAsync((ApplicationUser)null);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "password"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "User"))
            .ReturnsAsync(IdentityResult.Success);

        var request = new RegisterRequest("newuser", "new@test.com", "password", "IT");

        var result = await _authService.RegisterAsync(request);

        result.Should().NotBeNull();
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "password"), Times.Once);
    }

    [Fact]
    public async Task TwoFactorSetup_GeneratesValidSetup()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, Email = "test@test.com" };
        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString())).ReturnsAsync(user);

        var result = await _authService.SetupTwoFactorAsync(userId);

        result.Should().NotBeNull();
        result.SharedKey.Should().NotBeNullOrEmpty();
        result.QrCodeUri.Should().Contain("otpauth://totp/");
        result.RecoveryCodes.Should().HaveCount(10);
    }
}
