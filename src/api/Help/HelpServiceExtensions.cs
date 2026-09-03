namespace Intranet.Api.Help;

public static class HelpServiceExtensions
{
    public static IServiceCollection AddHelpAgent(this IServiceCollection services)
    {
        services.AddScoped<IHelpLlm, OllamaHelpLlm>();
        services.AddScoped<HelpAskService>();
        return services;
    }
}
