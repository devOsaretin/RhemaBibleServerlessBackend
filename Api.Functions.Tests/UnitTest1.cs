using System.Net;
using System.Reflection;
using System.Collections;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Functions.Tests;

public sealed class AiTypedSseTests
{
    [Fact]
    public async Task StreamQueryAiServiceTypedEvents_UsesResponseWrapper_WhenPresent()
    {
        var response = new FakeHttpResponseData();

        var json = JsonSerializer.SerializeToElement(new
        {
            response = new
            {
                text = "Hello",
                theologicalMeaning = "Meaning",
                historicalContext = "Context",
                devotionalInsight = "Devotional",
                originalLanguageInsights = new[] { "Greek" },
                practicalApplications = new[] { "Do this", "Do that" }
            }
        });

        await InvokePrivateStaticTask(
            "StreamQueryAiServiceTypedEventsAsync",
            response,
            (object)json,
            CancellationToken.None);

        var output = response.BodyAsString();

        Assert.Contains("data: {\"type\":\"text\",\"delta\":\"Hello\"}", output);
        Assert.Contains("data: {\"type\":\"theologicalMeaning\",\"delta\":\"Meaning\"}", output);
        Assert.Contains("data: {\"type\":\"historicalContext\",\"delta\":\"Context\"}", output);
        Assert.Contains("data: {\"type\":\"devotionalInsight\",\"delta\":\"Devotional\"}", output);
        Assert.Contains("data: {\"type\":\"originalLanguageInsights\",\"index\":0,\"delta\":\"Greek\"}", output);
        Assert.Contains("data: {\"type\":\"practicalApplications\",\"index\":0", output);
        Assert.Contains("data: {\"type\":\"practicalApplications\",\"index\":1", output);
    }

    [Fact]
    public async Task WriteSseStreamAsync_EmitsTypedDelta_Usage_AndDone()
    {
        var response = new FakeHttpResponseData();

        var parts = GetParts();

        await InvokePrivateStaticTask(
            "WriteSseStreamAsync",
            response,
            parts,
            "text",
            CancellationToken.None);

        var output = response.BodyAsString();

        Assert.Contains("data: {\"aiUsage\":", output);
        Assert.Contains("data: {\"type\":\"text\",\"delta\":\"Hi\"}", output);
        Assert.Contains("data: {\"type\":\"done\"}", output);

        static async IAsyncEnumerable<AiStreamPart> GetParts()
        {
            yield return new AiStreamUsagePart(new AiUsageDto { IsUnlimited = true });
            yield return new AiStreamDeltaPart("Hi");
            yield return new AiStreamDonePart();
            await Task.CompletedTask;
        }
    }

    [Fact]
    public async Task QueryStreamExtractor_HandlesSplitKeysAcrossDeltas()
    {
        var response = new FakeHttpResponseData();

        await InvokePrivateStaticTask(
            "WriteTypedEventsFromQueryJsonStreamAsync",
            response,
            GetParts(),
            CancellationToken.None);

        var output = response.BodyAsString();

        Assert.Contains("\"type\":\"text\"", output);
        Assert.Contains("\"delta\":\"Hello", output);
        Assert.Contains("\"type\":\"theologicalMeaning\"", output);
        Assert.Contains("\"type\":\"done\"", output);

        static async IAsyncEnumerable<AiStreamPart> GetParts()
        {
            yield return new AiStreamUsagePart(new AiUsageDto { IsUnlimited = true });

            // Intentionally split "text" across chunks to ensure buffering works.
            yield return new AiStreamDeltaPart("{\"response\":{\"te");
            yield return new AiStreamDeltaPart("xt\":\"Hello\",\"theologicalMeaning\":\"Meaning\"}}");

            yield return new AiStreamDonePart();
            await Task.CompletedTask;
        }
    }

    private static async Task InvokePrivateStaticTask(string methodName, params object?[] args)
    {
        var m = typeof(AiFunctions)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(AiFunctions).FullName, methodName);

        var result = m.Invoke(null, args);
        if (result is Task t)
        {
            await t;
            return;
        }

        throw new InvalidOperationException($"Expected {methodName} to return Task.");
    }

    private sealed class FakeHttpResponseData : HttpResponseData
    {
        private readonly MemoryStream _body = new();

        public FakeHttpResponseData() : base(new FakeFunctionContext())
        {
            Headers = new HttpHeadersCollection();
        }

        public override HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public override HttpHeadersCollection Headers { get; set; }
        public override Stream Body { get => _body; set => throw new NotSupportedException(); }
        public override HttpCookies Cookies => throw new NotSupportedException();

        public string BodyAsString()
        {
            _body.Position = 0;
            return Encoding.UTF8.GetString(_body.ToArray());
        }
    }

    private sealed class FakeFunctionContext : FunctionContext
    {
        private readonly InvocationFeatures _features = new();

        public override string InvocationId => "test";
        public override string FunctionId => "test";
        public override TraceContext TraceContext => throw new NotSupportedException();
        public override BindingContext BindingContext => throw new NotSupportedException();
        public override RetryContext RetryContext => throw new NotSupportedException();
        public override IServiceProvider InstanceServices { get; set; } = new DefaultServiceProvider();
        public override FunctionDefinition FunctionDefinition => throw new NotSupportedException();
        public override IInvocationFeatures Features => _features;
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override CancellationToken CancellationToken => CancellationToken.None;

        private sealed class DefaultServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }

        private sealed class InvocationFeatures : IInvocationFeatures
        {
            private readonly Dictionary<Type, object> _features = new();

            T IInvocationFeatures.Get<T>() => _features.TryGetValue(typeof(T), out var v) ? (T)v : default!;

            void IInvocationFeatures.Set<T>(T instance) => _features[typeof(T)] = instance!;

            public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
