using Microsoft.Extensions.DependencyInjection;

namespace Runner.Logging;

public static class LoggingServiceExtensions
{
    public static IServiceCollection AddTaskLogging(this IServiceCollection services, string logDirectory, LogLevel consoleLevel = LogLevel.Info)
    {
        services.AddSingleton(sp => new ConsoleTaskLogger(consoleLevel));
        services.AddSingleton(sp => new FileTaskLogger(logDirectory));
        
        services.AddSingleton<ITaskLogger>(sp =>
        {
            var consoleLogger = sp.GetRequiredService<ConsoleTaskLogger>();
            var fileLogger = sp.GetRequiredService<FileTaskLogger>();
            return new AggregateTaskLogger(new ITaskLogger[] { consoleLogger, fileLogger });
        });

        return services;
    }
}
