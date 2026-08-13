using KnowledgeVault.Infrastructure.AI;
using KnowledgeVault.Providers.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

public sealed class IntentRouterTests
{
    [Fact]
    public async Task Keyword_layer_classifies_plan_without_calling_llm()
    {
        var llm = new CountingLlmProvider();
        var router = NewRouter(llm);
        var intent = await router.ClassifyAsync("show me the plan for the auth refactor story");
        Assert.Equal(IntentKind.FindPlan, intent);
        Assert.Equal(0, llm.Calls);
    }

    [Fact]
    public async Task Keyword_layer_classifies_review_without_calling_llm()
    {
        var llm = new CountingLlmProvider();
        var router = NewRouter(llm);
        var intent = await router.ClassifyAsync("what's the review status on the API doc?");
        Assert.Equal(IntentKind.FindReview, intent);
        Assert.Equal(0, llm.Calls);
    }

    [Fact]
    public async Task Keyword_layer_classifies_memory_without_calling_llm()
    {
        var llm = new CountingLlmProvider();
        var router = NewRouter(llm);
        var intent = await router.ClassifyAsync("what does our project memory say about deployment?");
        Assert.Equal(IntentKind.FindMemory, intent);
        Assert.Equal(0, llm.Calls);
    }

    [Fact]
    public async Task Unrecognized_input_falls_back_to_llm_and_returns_general_when_unparseable()
    {
        var llm = new StubLlmProvider("lol idk");
        var router = NewRouter(llm);
        var intent = await router.ClassifyAsync("hello there");
        Assert.Equal(IntentKind.GeneralQuestion, intent);
        Assert.Equal(1, llm.Calls);
    }

    [Fact]
    public async Task Llm_exception_falls_back_to_general_question()
    {
        var llm = new ThrowingLlmProvider();
        var router = NewRouter(llm);
        var intent = await router.ClassifyAsync("anything");
        Assert.Equal(IntentKind.GeneralQuestion, intent);
    }

    private static IntentRouter NewRouter(ILLMProvider llm) =>
        new(llm, Options.Create(new LlmOptions()), NullLogger<IntentRouter>.Instance);

    private sealed class CountingLlmProvider : ILLMProvider
    {
        public int Calls { get; private set; }
        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, double? temperature = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult("GeneralQuestion");
        }
        public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessage> messages, double? temperature = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return "GeneralQuestion";
        }
    }

    private sealed class StubLlmProvider : ILLMProvider
    {
        private readonly string _response;
        public int Calls { get; private set; }
        public StubLlmProvider(string response) { _response = response; }
        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, double? temperature = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_response);
        }
        public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessage> messages, double? temperature = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return _response;
        }
    }

    private sealed class ThrowingLlmProvider : ILLMProvider
    {
        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, double? temperature = null, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("LLM offline");
        public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessage> messages, double? temperature = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return "";
        }
    }
}
