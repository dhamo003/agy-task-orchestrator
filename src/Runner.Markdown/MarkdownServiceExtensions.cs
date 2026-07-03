using Microsoft.Extensions.DependencyInjection;
using Runner.Markdown.Parser;
using Runner.Markdown.State;
using Runner.Markdown.Writer;

namespace Runner.Markdown;

public static class MarkdownServiceExtensions
{
    public static IServiceCollection AddMarkdownEngine(this IServiceCollection services)
    {
        services.AddTransient<ITaskParser, MarkdownTaskParser>();
        services.AddTransient<ITaskWriter, MarkdownTaskWriter>();
        services.AddSingleton<IStateManager, JsonStateManager>();

        return services;
    }
}
