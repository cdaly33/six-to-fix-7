using Microsoft.AspNetCore.Components;

namespace SixToFix.Web.Navigation;

public interface ILoginNavigator
{
    void NavigateToDashboard();
    // forceLoad: true so the browser sends the auth cookie on the next SSR request.
    void NavigateTo(string? returnUrl);
}

public sealed class LoginNavigator(NavigationManager navigationManager) : ILoginNavigator
{
    public void NavigateToDashboard() => navigationManager.NavigateTo("/dashboard", forceLoad: true);
    public void NavigateTo(string? returnUrl) =>
        navigationManager.NavigateTo(returnUrl ?? "/dashboard", forceLoad: true);
}
