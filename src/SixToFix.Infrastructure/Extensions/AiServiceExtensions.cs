using Microsoft.Extensions.Http.Resilience;
using Polly;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.ExternalClients;

namespace SixToFix.Infrastructure.Extensions;

public static class AiServiceExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.AddResiliencePipeline("azure-openai", builder => builder.AddTimeout(TimeSpan.FromSeconds(60)).AddRetry(new Polly.Retry.RetryStrategyOptions { MaxRetryAttempts = 3, BackoffType = Polly.DelayBackoffType.Exponential, Delay = TimeSpan.FromSeconds(2), UseJitter = true, ShouldHandle = new Polly.PredicateBuilder().Handle<HttpRequestException>().Handle<TaskCanceledException>() }).AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions { FailureRatio = 0.5, SamplingDuration = TimeSpan.FromSeconds(60), MinimumThroughput = 3, BreakDuration = TimeSpan.FromSeconds(60) }));
        services.AddSingleton<IAIClient, AzureOpenAIClient>();
        services.AddSingleton<IBlobStorage, AzureBlobStorageClient>();
        services.AddSingleton<ISearchClient, AzureSearchClient>();
        return services;
    }
}
