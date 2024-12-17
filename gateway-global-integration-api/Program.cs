using BL.Gateway.EventBus.Abstractions;
using GlobalIntegrationApi.Hubs;
using GlobalIntegrationApi.IntegrationEvents.EventHandling;
using GlobalIntegrationApi.IntegrationEvents.Events;
using GlobalIntegrationApi.Queries;
using GlobalIntegrationApi.Services;
using HealthChecks.UI.Client;
using IntegrationEventLogEF.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.ApplicationInsights;
using BL.Gateway.Services.Common;
using System.Data.Common;

namespace GlobalIntegrationApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSignalR();

            var appInsightsConnectionString = String.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")) ? builder.Configuration.GetConnectionString("ApplicationInsightConnectionString") : Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

            // Configure application insight logging
            builder.Logging.AddApplicationInsights(
                    configureTelemetryConfiguration: (config) =>
                    config.ConnectionString = appInsightsConnectionString,
                    configureApplicationInsightsLoggerOptions: (options) => { }
                );

            builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("gatewayGlobalIntegrationAPI", LogLevel.Trace);

            //Adds the Event Bus required for integration events
            builder.AddServiceDefaults();

            var connectionString = Environment.GetEnvironmentVariable("SQL_DB_CONNECTION_STRING");
            if (String.IsNullOrEmpty(connectionString))
            {
                connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            }

            builder.Services.AddDbContext<GlobalIntegrationContext>(options => options.UseSqlServer(connectionString));
            //builder.Services.AddDbContext<GlobalIntegrationContext>(options =>
            //{
            //    options.UseSqlServer(connectionString,
            //        sqlServerOptionsAction: sqlOptions =>
            //        {
            //            sqlOptions.EnableRetryOnFailure(
            //                            maxRetryCount: 5,
            //                            maxRetryDelay: TimeSpan.FromSeconds(30),
            //                            errorNumbersToAdd: null);
            //        });
            //});

            builder.Services.AddScoped<IGlobalDataQueries>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<GlobalDataQueries>>();
                return new GlobalDataQueries(connectionString, logger);
            });

            builder.Services.AddTransient<Func<DbConnection, IIntegrationEventLogService>>(sp => (DbConnection c) => new IntegrationEventLogService(c));

            builder.Services.AddTransient<IGlobalIntegrationServices, GlobalIntegrationServices>();

            builder.Services.AddTransient<RsiMessagePublishedIntegrationEventHandler>();
            builder.Services.AddTransient<NewRsiMessageSubmittedIntegrationEventHandler>();
            builder.Services.AddTransient<NewRsiMessageRecievedIntegrationEventHandler>();
            builder.Services.AddTransient<RequestStatusChangedToCancelledIntegrationEventHandler>();

            // Add health checks
            var hcBuilder = builder.Services.AddHealthChecks();

            // Add Global Integration DB health check
            hcBuilder.AddSqlServer(
                connectionString,
                name: "Global Integration DB");

            // Get which message bus is being used and add the relevant health check
            if (string.Equals(builder.Configuration["EventBus:ProviderName"], "ServiceBus", StringComparison.OrdinalIgnoreCase))
            {
                // Add Azure Serice Bus health check
                hcBuilder.AddAzureServiceBusTopic(
                    builder.Configuration.GetRequiredConnectionString("EventBus"),
                    topicName: builder.Configuration["EventBus:HealthCheckTopicName"],
                    name: "Azure Service Bus");
            }
            else
            {
                // Add RabbitMQ health check
                hcBuilder.AddRabbitMQ(
                    builder.Configuration.GetRequiredConnectionString("EventBus"),
                    name: "RabbitMQ");
            }

            var signalRClientUrl = String.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLIENT_BASE_URL")) ? builder.Configuration["ClientUrls:GlobalIntegrationUI"] : Environment.GetEnvironmentVariable("CLIENT_BASE_URL");

            if (String.IsNullOrEmpty(signalRClientUrl)) {
                throw new Exception("SignalR Client URL not set");
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            //if (app.Environment.IsDevelopment())
            //{
                app.UseSwagger();
                app.UseSwaggerUI();
            //}
            app.UseCors(policy => policy.AllowAnyHeader().AllowAnyMethod().WithOrigins(signalRClientUrl).AllowCredentials());

            //app.UseCors("AllowSpecificOrigins");

            app.UseHttpsRedirection();

            app.UseAuthorization();

            //app.UseSerilogRequestLogging();

            app.MapControllers();

            var eventBus = app.Services.GetRequiredService<IEventBus>();
            eventBus.Subscribe<NewRsiMessageSubmittedIntegrationEvent, NewRsiMessageSubmittedIntegrationEventHandler>(NewRsiMessageSubmittedIntegrationEvent.EVENT_NAME);
            eventBus.Subscribe<NewRsiMessageRecievedIntegrationEvent, NewRsiMessageRecievedIntegrationEventHandler>(NewRsiMessageRecievedIntegrationEvent.EVENT_NAME);
            eventBus.Subscribe<RsiMessagePublishedIntegrationEvent, RsiMessagePublishedIntegrationEventHandler>(RsiMessagePublishedIntegrationEvent.EVENT_NAME);
            eventBus.Subscribe<RequestStatusChangedToCancelledIntegrationEvent, RequestStatusChangedToCancelledIntegrationEventHandler>(RequestStatusChangedToCancelledIntegrationEvent.EVENT_NAME);

            app.MapHub<StatusHub>("statusHub");
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
            app.Run();
        }
    }
}
