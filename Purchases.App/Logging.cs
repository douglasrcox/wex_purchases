using Serilog;
using Serilog.Events;

namespace Purchases.App;

public static class Logging
{
    public static void ConfigureLogger(WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        builder.Host.UseSerilog();
    }
}
