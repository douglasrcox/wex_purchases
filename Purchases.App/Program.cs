using Microsoft.Extensions.Caching.Memory;
using Polly;
using Purchases.App;
using Purchases.App.Persistence;
using Purchases.App.Rates;
using Purchases.App.Services;
using Purchases.App;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Logging.ConfigureLogger(builder);

// Services
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(opt =>
    {
        // Let DataAnnotations bubble into ProblemDetails automatically
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Memory cache
builder.Services.AddMemoryCache();

// LiteDB repo (single embedded file under ./data)
builder.Services.AddSingleton<IPurchaseRepository>(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    var dataDir = Path.Combine(env.ContentRootPath, "data");
    Directory.CreateDirectory(dataDir);
    var dbPath = Path.Combine(dataDir, "purchases.db");
    return new LiteDbPurchaseRepository(dbPath);
});

// HttpClient (typed) for the Treasury client + Polly resilience
builder.Services.AddHttpClient<TreasuryExchangeRateClient>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    http.Timeout = TimeSpan.FromSeconds(cfg.GetValue("Rates:HttpTimeoutSeconds", 20));
})
.AddResilienceHandler("exrates", builder =>
{
    // Retry: 4 attempts, exponential backoff + jitter on 5xx, 408, 429, and transient exceptions
    builder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 4,
        BackoffType = DelayBackoffType.Exponential,
        Delay = TimeSpan.FromMilliseconds(250),
        UseJitter = true,
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>()
            .Handle<OperationCanceledException>()
            .HandleResult(r =>
                (int)r.StatusCode >= 500 ||
                r.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                (int)r.StatusCode == 429)
    });

    // Circuit breaker: break for 30s after 5 handled failures
    builder.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
    {
        BreakDuration = TimeSpan.FromSeconds(30),
        SamplingDuration = TimeSpan.FromSeconds(10),
        MinimumThroughput = 5,
        FailureRatio = 1.0,
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>()
            .Handle<OperationCanceledException>()
            .HandleResult(r =>
                (int)r.StatusCode >= 500 ||
                r.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                (int)r.StatusCode == 429)
    });
});

// Decorate the exchange-rate client with an in-memory caching layer
builder.Services.AddSingleton<IExchangeRateClient>(sp =>
{
    var inner = sp.GetRequiredService<TreasuryExchangeRateClient>();
    var cache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<CachingExchangeRateClient>>();
    return new CachingExchangeRateClient(inner, cache, logger);
});

// Domain service
builder.Services.AddSingleton<PurchaseService>();

var app = builder.Build();

app.UseSerilogRequestLogging();          // nice request logs
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

// Centralized exception -> ProblemDetails
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (KeyNotFoundException ex)
    {
        Log.Warning(ex, "Not found");
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        await Results.Problem(title: "Not Found", detail: ex.Message, statusCode: 404).ExecuteAsync(ctx);
    }
    catch (ArgumentException ex)
    {
        Log.Warning(ex, "Validation error");
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await Results.Problem(title: "Validation error", detail: ex.Message, statusCode: 400).ExecuteAsync(ctx);
    }
    catch (InvalidOperationException ex)
    {
        Log.Warning(ex, "Business rule error");
        ctx.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await Results.Problem(title: "Conversion error", detail: ex.Message, statusCode: 422).ExecuteAsync(ctx);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unhandled server error");
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(title: "Server error", statusCode: 500).ExecuteAsync(ctx);
    }
});

app.Run();
