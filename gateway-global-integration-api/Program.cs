using Elastic.CommonSchema.Serilog;
using Elastic.Ingest.Elasticsearch;
using Elastic.Serilog.Sinks;
using EventBus.Abstractions;
using GlobalIntegrationApi.Hubs;
using GlobalIntegrationApi.IntegrationEvents.EventHandling;
using GlobalIntegrationApi.IntegrationEvents.Events;
using GlobalIntegrationApi.Queries;
using GlobalIntegrationApi.Services;
using IntegrationEventLogEF.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Services.Common;
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

            builder.Host.UseSerilog((context, configuration) =>
            {
                var httpAccessor = context.Configuration.Get<HttpContextAccessor>();
                configuration.ReadFrom.Configuration(context.Configuration)
                             .Enrich.WithEcsHttpContext(httpAccessor)
                             .Enrich.WithEnvironmentName()
                             .WriteTo.ElasticCloud(context.Configuration["ElasticCloud:CloudId"], context.Configuration["ElasticCloud:CloudUser"], context.Configuration["ElasticCloud:CloudPass"], opts =>
                             {
                                 opts.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName("gateway-global-int-api-new-logs");
                                 opts.BootstrapMethod = BootstrapMethod.Failure;
                             });
            });

            //Adds the Event Bus required for integration events
            builder.AddServiceDefaults();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
            var dbName = Environment.GetEnvironmentVariable("DB_NAME");
            var dbPassword = Environment.GetEnvironmentVariable("DB_SA_PASSWORD");

            if (connectionString != null)
            {
                connectionString = connectionString.Replace("{#host}", dbHost).Replace("{#dbName}", dbName).Replace("{#dbPassword}", dbPassword);
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

            app.UseSerilogRequestLogging();

            app.MapControllers();

            var eventBus = app.Services.GetRequiredService<IEventBus>();
            eventBus.Subscribe<NewRsiMessageSubmittedIntegrationEvent, NewRsiMessageSubmittedIntegrationEventHandler>(NewRsiMessageSubmittedIntegrationEvent.EVENT_NAME);
            eventBus.Subscribe<NewRsiMessageRecievedIntegrationEvent, NewRsiMessageRecievedIntegrationEventHandler>(NewRsiMessageRecievedIntegrationEvent.EVENT_NAME);
            eventBus.Subscribe<RsiMessagePublishedIntegrationEvent, RsiMessagePublishedIntegrationEventHandler>(RsiMessagePublishedIntegrationEvent.EVENT_NAME);

            app.MapHub<StatusHub>("statusHub");

            app.Run();
        }
    }
}
