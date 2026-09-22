using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;

namespace CulinaryBlog.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IGoogleTokenValidator _googleTokenValidator;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IGoogleTokenValidator googleTokenValidator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _googleTokenValidator = googleTokenValidator;
    }

    public async Task<RegisterResult> RegisterUserAsync(
        string email,
        string password,
        string displayName,
        string role,
        CancellationToken cancellationToken = default)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            return new RegisterResult(
                Succeeded: false,
                UserId: null,
                Email: null,
                DisplayName: null,
                ErrorMessage: "An account with this email already exists.",
                IsDuplicateEmail: true);
        }

        // CONFLICT-009 / Chapter 8 temporary mapping: UserName maps to Email
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return new RegisterResult(false, null, null, null, errors);
        }

        // Ensure role exists and assign (FR-AUTH-001: new user gets Author role)
        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new IdentityRole(role));
        }

        await _userManager.AddToRoleAsync(user, role);

        return new RegisterResult(
            Succeeded: true,
            UserId: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            ErrorMessage: null);
    }

    public async Task<LoginResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Generic 401 response to avoid user enumeration
            return new LoginResult(
                Succeeded: false,
                UserId: null,
                Email: null,
                Roles: null,
                ErrorMessage: "Invalid email or password.");
        }

        // Check if user is currently locked out (FR-AUTH-002)
        if (await _userManager.IsLockedOutAsync(user))
        {
            return new LoginResult(
                Succeeded: false,
                UserId: user.Id,
                Email: user.Email,
                Roles: null,
                IsLockedOut: true,
                LockoutEnd: user.LockoutEnd,
                ErrorMessage: "Account is locked due to multiple failed login attempts.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            // Increment failed attempt counter; triggers lockout after 5 failed attempts
            await _userManager.AccessFailedAsync(user);

            if (await _userManager.IsLockedOutAsync(user))
            {
                return new LoginResult(
                    Succeeded: false,
                    UserId: user.Id,
                    Email: user.Email,
                    Roles: null,
                    IsLockedOut: true,
                    LockoutEnd: user.LockoutEnd,
                    ErrorMessage: "Account is locked due to multiple failed login attempts.");
            }

            return new LoginResult(
                Succeeded: false,
                UserId: null,
                Email: null,
                Roles: null,
                ErrorMessage: "Invalid email or password.");
        }

        // Login success: reset access failed count
        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        return new LoginResult(
            Succeeded: true,
            UserId: user.Id,
            Email: user.Email,
            Roles: roles);
    }

    public async Task<GoogleAuthResult> AuthenticateGoogleUserAsync(
        string idToken,
        string defaultRole,
        CancellationToken cancellationToken = default)
    {
        var tokenInfo = await _googleTokenValidator.ValidateAsync(idToken, cancellationToken);
        if (tokenInfo == null)
        {
            return new GoogleAuthResult(
                Succeeded: false,
                UserId: null,
                Email: null,
                Roles: null,
                ErrorMessage: "Invalid Google token.",
                IsInvalidToken: true);
        }

        var user = await _userManager.FindByLoginAsync("Google", tokenInfo.Subject);

        if (user == null)
        {
            // Check if user with this email already exists
            user = await _userManager.FindByEmailAsync(tokenInfo.Email);

            if (user != null)
            {
                // Link Google login to existing account
                await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", tokenInfo.Subject, "Google"));
            }
            else
            {
                // Create new user with Google profile
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = tokenInfo.Email,
                    Email = tokenInfo.Email,
                    DisplayName = tokenInfo.Name,
                    AvatarUrl = tokenInfo.Picture,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                    return new GoogleAuthResult(false, null, null, null, errors);
                }

                if (!await _roleManager.RoleExistsAsync(defaultRole))
                {
                    await _roleManager.CreateAsync(new IdentityRole(defaultRole));
                }

                await _userManager.AddToRoleAsync(user, defaultRole);
                await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", tokenInfo.Subject, "Google"));
            }
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new GoogleAuthResult(
            Succeeded: true,
            UserId: user.Id,
            Email: user.Email,
            Roles: roles);
    }
}
