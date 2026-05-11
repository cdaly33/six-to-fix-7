using Microsoft.AspNetCore.Components;

namespace SixToFix.Web.Navigation;

public interface ILoginNavigator
{
    void NavigateToDashboard();
}

public sealed class LoginNavigator(NavigationManager navigationManager) : ILoginNavigator
{
    public void NavigateToDashboard() => navigationManager.NavigateTo("/dashboard", forceLoad: false);
}
