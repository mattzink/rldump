using System.Net;
using System.Text.Json;
using Rldump.Api;
using Rldump.Tests.Fakes;

namespace Rldump.Tests;

public class RocketPlanetClientTests
{
    private const string BaseUrl = "https://prime.rocketplanet.gg";
    private const string Token = "test-token-123";

    private static RocketPlanetClient NewClient(FakeHttpHandler handler) =>
        new(BaseUrl, Token, TimeSpan.FromSeconds(5), handler);

    [Fact]
    public async Task GetSchedule_SendsQueryVerbUrlHeadersAndExactBody()
    {
        var handler = FakeHttpHandler.JsonResponse("""{ "Result": { "Schedules": [] } }""");
        RocketPlanetClient client = NewClient(handler);

        GetScheduleResult result = await client.QueryAsync(
            ApiPaths.GetSchedule, new GetScheduleRequest { Region = "USW" },
            ApiJsonContext.Default.GetScheduleRequest, ApiJsonContext.Default.EnvelopeGetScheduleResult,
            TestContext.Current.CancellationToken);

        Assert.Empty(result.Schedules);

        FakeHttpHandler.Request request = Assert.Single(handler.Requests);
        Assert.Equal("QUERY", request.Method);
        Assert.Equal(new Uri(BaseUrl + ApiPaths.GetSchedule), request.Uri);
        Assert.Equal("Bearer " + Token, request.Authorization);
        Assert.Equal("application/json", request.ContentType);
        Assert.Equal("""{"Region":"USW"}""", request.Body);
    }

    [Fact]
    public async Task GetScheduleRegions_SendsEmptyObjectBody()
    {
        var handler = FakeHttpHandler.JsonResponse("""{ "Result": [] }""");
        RocketPlanetClient client = NewClient(handler);

        JsonElement result = await client.QueryOpaqueAsync(
            ApiPaths.GetScheduleRegions, new GetScheduleRegionsRequest(),
            ApiJsonContext.Default.GetScheduleRegionsRequest, ApiJsonContext.Default.EnvelopeJsonElement,
            TestContext.Current.CancellationToken);

        Assert.Equal(JsonValueKind.Array, result.ValueKind);

        FakeHttpHandler.Request request = Assert.Single(handler.Requests);
        Assert.Equal("QUERY", request.Method);
        Assert.Equal(new Uri(BaseUrl + ApiPaths.GetScheduleRegions), request.Uri);
        Assert.Equal("{}", request.Body);
    }

    [Fact]
    public async Task SuccessEnvelope_ReturnsResult()
    {
        var handler = FakeHttpHandler.JsonResponse(
            """{ "Result": { "Schedules": [ { "Time": 0, "ScheduleID": 1, "bUpdateSkill": false, "Tournaments": [] } ] } }""");
        RocketPlanetClient client = NewClient(handler);

        GetScheduleResult result = await client.QueryAsync(
            ApiPaths.GetSchedule, new GetScheduleRequest { Region = "USW" },
            ApiJsonContext.Default.GetScheduleRequest, ApiJsonContext.Default.EnvelopeGetScheduleResult,
            TestContext.Current.CancellationToken);

        Assert.Single(result.Schedules);
        Assert.Equal(1, result.Schedules[0].ScheduleID);
    }

    [Fact]
    public async Task ErrorEnvelope_ThrowsApiErrorException()
    {
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("getschedule-error.json"));
        RocketPlanetClient client = NewClient(handler);

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(() =>
            client.QueryAsync(ApiPaths.GetSchedule, new GetScheduleRequest { Region = "XYZ" },
                ApiJsonContext.Default.GetScheduleRequest, ApiJsonContext.Default.EnvelopeGetScheduleResult,
                TestContext.Current.CancellationToken));

        Assert.Equal("ScheduleRegionIsInvalid", ex.Type);
        Assert.Equal("The schedule region 'XYZ' is not valid.", ex.Message);
    }

    [Fact]
    public async Task ErrorEnvelope_RedactsTokenInMessage()
    {
        string body = $$"""{ "Error": { "Message": "echo {{Token}}", "Type": "NotLoggedIn" } }""";
        var handler = FakeHttpHandler.JsonResponse(body);
        RocketPlanetClient client = NewClient(handler);

        ApiErrorException ex = await Assert.ThrowsAsync<ApiErrorException>(() =>
            client.QueryAsync(ApiPaths.GetSchedule, new GetScheduleRequest { Region = "USW" },
                ApiJsonContext.Default.GetScheduleRequest, ApiJsonContext.Default.EnvelopeGetScheduleResult,
                TestContext.Current.CancellationToken));

        Assert.DoesNotContain(Token, ex.Message);
        Assert.Contains("[redacted]", ex.Message);
    }

    [Fact]
    public async Task Non2xx_ThrowsApiHttpException_WithRedactedBody()
    {
        var handler = FakeHttpHandler.Status(HttpStatusCode.ServiceUnavailable, $"upstream failure, saw token {Token}");
        RocketPlanetClient client = NewClient(handler);

        ApiHttpException ex = await Assert.ThrowsAsync<ApiHttpException>(() =>
            client.QueryRawAsync(ApiPaths.GetSchedule, new GetScheduleRequest { Region = "USW" },
                ApiJsonContext.Default.GetScheduleRequest, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Contains("HTTP 503", ex.Message);
        Assert.Contains(ApiPaths.GetSchedule, ex.Message);
        Assert.Contains("[redacted]", ex.Message);
        Assert.DoesNotContain(Token, ex.Message);
    }

    [Fact]
    public async Task BaseUrl_TrailingSlashIsTrimmed()
    {
        var handler = FakeHttpHandler.JsonResponse("""{ "Result": { "Schedules": [] } }""");
        RocketPlanetClient client = new("https://prime.rocketplanet.gg/", Token, TimeSpan.FromSeconds(5), handler);

        await client.QueryRawAsync(ApiPaths.GetSchedule, new GetScheduleRequest { Region = "USW" },
            ApiJsonContext.Default.GetScheduleRequest, TestContext.Current.CancellationToken);

        Assert.Equal(new Uri(BaseUrl + ApiPaths.GetSchedule), handler.Requests[0].Uri);
    }
}
