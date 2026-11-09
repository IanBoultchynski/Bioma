using Bioma.Application.DTOs;

namespace Bioma.Client;

/// <summary>
/// État de session de l'utilisateur connecté dans l'application Blazor.
/// </summary>
public class UserSessionState
{
    public UserProfileDto? CurrentProfile { get; private set; }
    public string? AuthToken { get; private set; }

    public bool IsAuthenticated => CurrentProfile is not null;

    public event Action? OnChange;

    public void SetSession(UserProfileDto profile, string? token = null)
    {
        CurrentProfile = profile;
        if (token is not null)
        {
            AuthToken = token;
        }
        OnChange?.Invoke();
    }

    public void ClearSession()
    {
        CurrentProfile = null;
        AuthToken = null;
        OnChange?.Invoke();
    }
}
