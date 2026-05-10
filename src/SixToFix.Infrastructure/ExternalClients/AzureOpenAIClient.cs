using Azure.Identity;
using OpenAI.Chat;
using SixToFix.Application.Models;
using SixToFix.Application.Services;

namespace SixToFix.Infrastructure.ExternalClients;

public sealed class AzureOpenAIClient : IAIClient
{
    private readonly Azure.AI.OpenAI.AzureOpenAIClient _client;
    private readonly string _deploymentName;

    public AzureOpenAIClient(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not configured.");

        _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o";
        _client = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
    }

    public async Task<AiCompletionResult> CompleteAsync(
        string skillName,
        string systemPrompt,
        string userPrompt,
        string outputSchemaJson,
        CancellationToken ct = default)
    {
        ChatClient chatClient = _client.GetChatClient(_deploymentName);
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                skillName,
                BinaryData.FromString(outputSchemaJson),
                jsonSchemaIsStrict: true)
        };

        var completion = await chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            ],
            options,
            ct);

        var content = completion.Value.Content.Count > 0
            ? string.Concat(completion.Value.Content.Select(part => part.Text))
            : string.Empty;

        return new AiCompletionResult(
            Content: content,
            TokensUsed: completion.Value.Usage?.TotalTokenCount ?? 0,
            ModelId: completion.Value.Model,
            FinishReason: completion.Value.FinishReason.ToString());
    }
}
