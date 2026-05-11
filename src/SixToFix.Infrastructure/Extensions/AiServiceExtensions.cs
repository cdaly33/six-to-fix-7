using System.Net.Http.Headers;
using System.Threading.Channels;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using SixToFix.Application.Models;
using SixToFix.Application.Services;
using SixToFix.Infrastructure.BackgroundServices;
using SixToFix.Infrastructure.ExternalClients;
using SixToFix.Infrastructure.Services;

namespace SixToFix.Infrastructure.Extensions;

public static class AiServiceExtensions
{
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddResiliencePipeline("azure-openai", builder =>
        {
            builder
                .AddTimeout(TimeSpan.FromSeconds(60))
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromSeconds(2),
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(60),
                    MinimumThroughput = 3,
                    BreakDuration = TimeSpan.FromSeconds(60)
                });
        });

        // Singleton: stateless file reader, safe for concurrent access. Scoped SkillRunner → Singleton ISkillLoader is the safe one-way direction per ADR-001.
        services.AddSingleton<ISkillLoader, SkillLoader>();

        services.AddScoped<ISkillRunner, SkillRunner>();
        services.AddScoped<IPolicyEngine, PolicyEngine>();
        services.AddScoped<ICouncilRunner, CouncilRunner>();

        services.AddSingleton<IAIClient, AzureOpenAIClient>();
        services.AddSingleton<IBlobStorage, AzureBlobStorageClient>();
        services.AddSingleton<ISearchClient, AzureSearchClient>();
        services.AddHttpClient<IHubSpotClient, HubSpotClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.hubapi.com/");
                var privateAppToken = configuration["HubSpot:PrivateAppToken"];
                if (!string.IsNullOrWhiteSpace(privateAppToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", privateAppToken);
                }
            })
            .AddStandardResilienceHandler();

        services.AddSingleton(_ => Channel.CreateUnbounded<HubSpotEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        }));

        services.AddHostedService<HubSpotWorker>();

        return services;
    }
}
