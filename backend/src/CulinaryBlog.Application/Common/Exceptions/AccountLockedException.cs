namespace CulinaryBlog.Application.Common.Exceptions;

public class AccountLockedException : Exception
{
    public DateTimeOffset? LockoutEnd { get; }

    public AccountLockedException(DateTimeOffset? lockoutEnd = null)
        : base("Account is locked due to multiple failed login attempts. Please try again later.")
    {
        LockoutEnd = lockoutEnd;
    }
}
