using System.Net;
using CSharpFunctionalExtensions;
using Sunrise.Shared.Enums;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Objects.Serializable.Performances;
using Sunrise.Shared.Objects.Sessions;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Services;

namespace Sunrise.Tests.Services.Mock;

public class MockHttpClientService(RedisRepository redis) : HttpClientService(redis)
{
    private readonly Dictionary<ApiType, Func<object, object>> _mockResponses = new();

    public void MockResponse<TResponse>(ApiType apiType, Func<object, TResponse> responseFactory)
    {
        _mockResponses[apiType] = args => responseFactory(args)!;
    }

    public void MockPerformanceCalculation(double performancePoints = 500, double difficultyRating = 5.0)
    {
        MockResponse<PerformanceAttributes>(ApiType.CalculateScorePerformance,
            _ => new PerformanceAttributes
            {
                PerformancePoints = performancePoints,
                Difficulty = new DifficultyAttributes
                {
                    Stars = difficultyRating,
                    MaxCombo = 200,
                    Mode = GameMode.Standard
                },
                State = new ScoreState
                {
                    MaxCombo = 200,
                    N300 = 150,
                    N100 = 10,
                    N50 = 0,
                    NGeki = 20,
                    NKatu = 5,
                    Misses = 0
                }
            });

        MockResponse<List<PerformanceAttributes>>(ApiType.CalculateBeatmapPerformance,
            _ => new List<PerformanceAttributes>
            {
                new()
                {
                    PerformancePoints = performancePoints,
                    Difficulty = new DifficultyAttributes
                    {
                        Stars = difficultyRating,
                        MaxCombo = 200,
                        Mode = GameMode.Standard
                    },
                    State = new ScoreState
                    {
                        MaxCombo = 200,
                        N300 = 150,
                        N100 = 10,
                        N50 = 0,
                        NGeki = 20,
                        NKatu = 5,
                        Misses = 0
                    }
                }
            });
    }

    public override Task<Result<T, ErrorMessage>> PostRequestWithBody<T>(BaseSession session, ApiType type, object body, Dictionary<string, string>? headers = null)
    {
        if (_mockResponses.TryGetValue(type, out var mockResponse))
        {
            try
            {
                var response = mockResponse(body);
                return Task.FromResult(Result.Success<T, ErrorMessage>((T)response));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result.Failure<T, ErrorMessage>(new ErrorMessage
                {
                    Message = $"Mock failed: {ex.Message}",
                    Status = HttpStatusCode.InternalServerError
                }));
            }
        }

        return base.PostRequestWithBody<T>(session, type, body, headers);
    }

    public override Task<Result<T, ErrorMessage>> SendRequest<T>(BaseSession session, ApiType type, object?[] args, Dictionary<string, string>? headers = null, CancellationToken ct = default)
    {
        if (_mockResponses.TryGetValue(type, out var mockResponse))
        {
            try
            {
                var response = mockResponse(args);
                return Task.FromResult(Result.Success<T, ErrorMessage>((T)response));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result.Failure<T, ErrorMessage>(new ErrorMessage
                {
                    Message = $"Mock failed: {ex.Message}",
                    Status = HttpStatusCode.InternalServerError
                }));
            }
        }

        return base.SendRequest<T>(session, type, args, headers, ct);
    }

    public void ClearMocks()
    {
        _mockResponses.Clear();
    }
}