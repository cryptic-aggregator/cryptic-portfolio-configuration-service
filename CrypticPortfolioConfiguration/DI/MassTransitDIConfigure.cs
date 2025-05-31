using CrypticPortfolioConfiguration.Interfaces.Config;
using MassTransit;

namespace CrypticPortfolioConfiguration.DI;

public static class MassTransitDIConfigure
{
    public static IServiceCollection AddMassTransitWithRabbitMQ(this IServiceCollection services, IRabbitMQConfig rabbitMqConfig)
    {
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitMqConfig.RabbitMQ__HostName, rabbitMqConfig.RabbitMQ__Port, rabbitMqConfig.RabbitMQ__VHost, h =>
                {
                    h.Username(rabbitMqConfig.RabbitMQ__Username);
                    h.Password(rabbitMqConfig.RabbitMQ__Password);
                });
            });
        });
        
        return services;
    }
}