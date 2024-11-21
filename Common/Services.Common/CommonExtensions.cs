using EventBus;
using EventBus.Abstractions;
using EventBusRabbitMQ;
using EventBusServiceBus;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
namespace Services.Common;

public static class CommonExtensions
{
    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder)
    {
        // Shared configuration via key vault
        //builder.Configuration.AddKeyVault();

        // Shared app insights configuration
        //builder.Services.AddApplicationInsights(builder.Configuration);

        // Default health checks assume the event bus and self health checks
        //builder.Services.AddDefaultHealthChecks(builder.Configuration);

        // Add the event bus
        builder.Services.AddEventBus(builder.Configuration);

        //builder.Services.AddDefaultAuthentication(builder.Configuration);

        //builder.Services.AddDefaultOpenApi(builder.Configuration);

        // Add the accessor
        builder.Services.AddHttpContextAccessor();

        return builder;
    }

    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        //  {
        //    "ConnectionStrings": {
        //      "EventBus": "..."
        //    },

        // {
        //   "EventBus": {
        //     "ProviderName": "ServiceBus | RabbitMQ",
        //     ...
        //   }
        // }

        // {
        //   "EventBus": {
        //     "ProviderName": "ServiceBus",
        //     "SubscriptionClientName": "eshop_event_bus"
        //   }
        // }

        // {
        //   "EventBus": {
        //     "ProviderName": "RabbitMQ",
        //     "SubscriptionClientName": "...",
        //     "UserName": "...",
        //     "Password": "...",
        //     "RetryCount": 1
        //   }
        // }

        var eventBusSection = configuration.GetSection("EventBus");

        if (!eventBusSection.Exists())
        {
            return services;
        }

        //Connection for RabbitMQ connection when running locally in isolation or in docker compose environment
        var rabbitBusHost = String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSG_HOST")) ? 
                                        eventBusSection["HostName"] : Environment.GetEnvironmentVariable("MSG_HOST");

        if (string.Equals(eventBusSection["ProviderName"], "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IServiceBusPersisterConnection>(sp =>
            {
                //check the Environment vars first or a connection string and if not get it from config
                var serviceBusConnectionString = String.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_CONNECTION_STRING")) ? configuration.GetRequiredConnectionString("EventBus") : Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_CONNECTION_STRING");
                if (String.IsNullOrEmpty(serviceBusConnectionString))
                {
                    throw new Exception("No Service Bus Connection defined");
                }
                return new DefaultServiceBusPersisterConnection(serviceBusConnectionString);
            });

            services.AddSingleton<IEventBus, EventBusServiceBus.EventBusServiceBus>(sp =>
            {
                var serviceBusPersisterConnection = sp.GetRequiredService<IServiceBusPersisterConnection>();
                var logger = sp.GetRequiredService<ILogger<EventBusServiceBus.EventBusServiceBus>>();
                var eventBusSubscriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                string subscriptionName = eventBusSection.GetRequiredValue("SubscriptionClientName");

                return new EventBusServiceBus.EventBusServiceBus(serviceBusPersisterConnection, logger,
                    eventBusSubscriptionsManager, sp, subscriptionName);
            });
        }
        else
        {
            services.AddSingleton<IDefaultRabbitMQPersistentConnection>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();

                var factory = new ConnectionFactory()
                {
                    HostName = rabbitBusHost,
                    DispatchConsumersAsync = true
                };

                if (!string.IsNullOrEmpty(eventBusSection["UserName"]))
                {
                    factory.UserName = eventBusSection["UserName"];
                }

                if (!string.IsNullOrEmpty(eventBusSection["Password"]))
                {
                    factory.Password = eventBusSection["Password"];
                }

                var retryCount = eventBusSection.GetValue("RetryCount", 5);

                return new DefaultRabbitMQPersistentConnection(factory, logger, retryCount);
            });

            services.AddSingleton<IEventBus, EventBusRabbitMQ.EventBusRabbitMQ>(sp =>
            {
                var subscriptionClientName = eventBusSection.GetRequiredValue("SubscriptionClientName");
                var rabbitMQPersistentConnection = sp.GetRequiredService<IDefaultRabbitMQPersistentConnection>();
                var logger = sp.GetRequiredService<ILogger<EventBusRabbitMQ.EventBusRabbitMQ>>();
                var eventBusSubscriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();
                var retryCount = eventBusSection.GetValue("RetryCount", 5);
                var globalIntegrationQueueName = eventBusSection.GetValue("GlobalIntegrationQueueName", String.Empty);
                var globalIntegrationRoutingKey = eventBusSection.GetValue("GlobalIntegrationRoutingKey", String.Empty);

                return new EventBusRabbitMQ.EventBusRabbitMQ(rabbitMQPersistentConnection, logger, sp, eventBusSubscriptionsManager, subscriptionClientName, globalIntegrationRoutingKey, globalIntegrationQueueName, retryCount);
            });
        }

        services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();
        return services;
    }
}
