using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Authentication;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace CulinaryBlog.Application.Features.Auth.ChangePassword;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IIdentityService _identityService;

    public ChangePasswordCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.ChangePasswordAsync(
            request.UserId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (result.IsIncorrectCurrentPassword)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.CurrentPassword), "Mật khẩu hiện tại không chính xác.")
            });
        }

        if (!result.Succeeded)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.NewPassword), result.ErrorMessage ?? "Đổi mật khẩu thất bại.")
            });
        }
    }
}
