using System.Runtime.CompilerServices;

// Allow SixToFix.Web to call internal members (e.g., HttpContextTenantContext.Resolve)
[assembly: InternalsVisibleTo("SixToFix.Web")]
// Allow Infrastructure test project to access DesignTimeTenantContext for integration test setup
[assembly: InternalsVisibleTo("SixToFix.Infrastructure.Tests")]
