using System;
using System.Reflection;
using System.Text.Json.Serialization;
using dotnet.Common.HealthChecks;
using dotnet.Common.Identity;
using dotnet.Common.Logging;
using dotnet.Common.MassTransit;
using dotnet.Common.MongoDB;
using dotnet.Common.OpenTelemetry;
using dotnet.Common.Settings;
using dotnet.Identity.Contracts;
using dotnet.Inventory.Contracts;
using dotnet.Trading.Service.Entities;
using dotnet.Trading.Service.Exceptions;
using dotnet.Trading.Service.Settings;
using dotnet.Trading.Service.SignalR;
using dotnet.Trading.Service.StateMachines;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace dotnet.Trading.Service
{
    public class Startup
    {
        private const string AllowedOriginSetting = "AllowedOrigin";
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMongo()
                    .AddMongoRepository<CatalogItem>("catalogitems")
                    .AddMongoRepository<InventoryItem>("inventoryitems")
                    .AddMongoRepository<ApplicationUser>("users")
                    .AddJwtBearerAuthentication();
            AddMassTransit(services);

            services.AddControllers(options =>
            {
                options.SuppressAsyncSuffixInActionNames = false;
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "dotnet.Trading.Service", Version = "v1" });
            });

            services.AddSingleton<IUserIdProvider, UserIdProvider>()
                    .AddSingleton<MessageHub>()
                    .AddSignalR();

            services.AddHealthChecks()
                    .AddMongoDb();

            services.AddSeqLogging(Configuration)
                    .AddTracing(Configuration);

            services.AddOpenTelemetryMetrics(builder =>
            {
                var settings = Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
                builder.AddMeter(settings.ServiceName)
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation()
                        .AddPrometheusExporter();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "dotnet.Trading.Service v1"));

                app.UseCors(builder =>
                {
                    builder.WithOrigins(Configuration[AllowedOriginSetting])
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            }

            app.UseOpenTelemetryPrometheusScrapingEndpoint();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<MessageHub>("/messagehub");
                endpoints.MapDotnetEconomyHealthChecks();
            });
        }

        private void AddMassTransit(IServiceCollection services)
        {
            services.AddMassTransit(configure =>
            {
                configure.usingDotnetEconomyMessageBroker(Configuration, retryConfigurator =>
                {
                    retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
                    retryConfigurator.Ignore(typeof(UnknownItemException));
                });
                //Ensures all consumers found in the Entry Assembly(Assembly that starts the app)
                // are added with MT, so they can react when messages arrive 
                configure.AddConsumers(Assembly.GetEntryAssembly());
                configure.AddSagaStateMachine<PurchaseStateMachine, PurchaseState>(sagaConfigurator => { sagaConfigurator.UseInMemoryOutbox(); })
                        .MongoDbRepository(repo =>
                        {
                            var serviceSettings = Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
                            var mongoSettings = Configuration.GetSection(nameof(MongoDbSettings)).Get<MongoDbSettings>();
                            repo.Connection = mongoSettings.ConnectionString;
                            repo.DatabaseName = serviceSettings.ServiceName;
                        });
            });

            var queueSettings = Configuration.GetSection(nameof(QueueSettings)).Get<QueueSettings>();
            EndpointConvention.Map<GrantItems>(new Uri(queueSettings.GrantItemsQueueAddress));
            EndpointConvention.Map<ComotOkubo>(new Uri(queueSettings.ComotOkuboQueueAddress));
            EndpointConvention.Map<SubtractItems>(new Uri(queueSettings.SubtractItemsQueueAddress));

        }
    }
}
