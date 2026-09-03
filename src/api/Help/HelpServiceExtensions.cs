namespace Intranet.Api.Help;

public static class HelpServiceExtensions
{
    public static IServiceCollection AddHelpAgent(this IServiceCollection services)
    {
        services.AddScoped<IHelpLlm, HelpLlm>();
        services.AddScoped<HelpAskService>();
        return services;
    }
}
