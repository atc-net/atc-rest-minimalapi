namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

/// <summary>A response of the validation behaviour suite.</summary>
/// <param name="Status">The status code.</param>
/// <param name="ContentType">The media type of the response.</param>
/// <param name="Body">The raw body.</param>
public sealed record ValidationBehaviourResponse(
    int Status,
    string? ContentType,
    string Body);

public static class ValidationBehaviourClient
{
    public const string ValidationTitle = "One or more validation errors occurred.";
    public const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    public static Task<ValidationBehaviourResponse> PostJsonAsync(
        this HttpClient client,
        string path,
        string? json,
        Action<HttpRequestMessage>? configureRequest = null)
        => client.PostAsync(path, json, "application/json", configureRequest);

    public static async Task<ValidationBehaviourResponse> PostAsync(
        this HttpClient client,
        string path,
        string? body,
        string contentType,
        Action<HttpRequestMessage>? configureRequest = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = new StringContent(body);
            request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        }

        configureRequest?.Invoke(request);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return new ValidationBehaviourResponse(
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Asserts the full validation 400 body: status, <c>Content-Type</c>, <c>type</c>, <c>title</c>, and exactly the
    /// given <c>errors</c> (every key with all its messages). Keys and the messages of a key are compared without order:
    /// neither engine promises an order.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="errors">The expected errors: one entry per message; a key with two messages appears twice.</param>
    public static void ShouldBeValidationProblem(
        this ValidationBehaviourResponse response,
        params (string Key, string Message)[] errors)
    {
        response.Status.Should().Be(StatusCodes.Status400BadRequest, response.Body);
        response.ContentType.Should().Be("application/problem+json", response.Body);

        using var document = JsonDocument.Parse(response.Body);
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status400BadRequest, response.Body);
        root.GetProperty("type").GetString().Should().Be(BadRequestType, response.Body);
        root.GetProperty("title").GetString().Should().Be(ValidationTitle, response.Body);

        var actual = root
            .GetProperty("errors")
            .EnumerateObject()
            .ToDictionary(
                p => p.Name,
                p => p.Value.EnumerateArray().Select(m => m.GetString()!).ToArray(),
                StringComparer.Ordinal);

        var expected = errors
            .GroupBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray(), StringComparer.Ordinal);

        // Keys first, then each key's messages: a dictionary comparison counts the dots of a key as nesting depth.
        actual.Keys.Should().BeEquivalentTo(expected.Keys, response.Body);
        foreach (var (key, messages) in expected)
        {
            actual[key].Should().BeEquivalentTo(messages, $"the messages of '{key}' in {response.Body}");
        }
    }

    public static void ShouldBeOk(this ValidationBehaviourResponse response)
        => response.Status.Should().Be(StatusCodes.Status200OK, response.Body);
}