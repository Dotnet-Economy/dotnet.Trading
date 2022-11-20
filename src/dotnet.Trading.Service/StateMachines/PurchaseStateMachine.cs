using System;
using dotnet.Identity.Contracts;
using dotnet.Inventory.Contracts;
using dotnet.Trading.Service.Activities;
using dotnet.Trading.Service.Contracts;
using dotnet.Trading.Service.SignalR;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace dotnet.Trading.Service.StateMachines
{
    public class PurchaseStateMachine : MassTransitStateMachine<PurchaseState>
    {

        private readonly MessageHub hub;
        private readonly ILogger<PurchaseStateMachine> logger;
        public State Accepted { get; }
        public State ItemsGranted { get; }
        public State Completed { get; }
        public State Faulted { get; }

        public Event<PurchaseRequested> PurchaseRequested { get; }
        public Event<GetPurchaseState> GetPurchaseState { get; }
        public Event<InventoryItemsGranted> InventoryItemsGranted { get; }
        public Event<OkuboDonComot> OkuboDonComot { get; }
        public Event<Fault<GrantItems>> GrantItemsFaulted { get; }
        public Event<Fault<ComotOkubo>> ComotOkuboFaulted { get; }

        public PurchaseStateMachine(MessageHub hub, ILogger<PurchaseStateMachine> logger)
        {
            InstanceState(state => state.CurrentState);
            ConfigureEvents();
            ConfigureInitialState();
            ConfigureAny();
            ConfigureAccepted();
            ConfigureItemsGranted();
            ConfigureFaulted();
            ConfigureCompleted();
            this.hub = hub;
            this.logger = logger;
        }

        private void ConfigureEvents()
        {
            Event(() => PurchaseRequested);
            Event(() => GetPurchaseState);
            Event(() => InventoryItemsGranted);
            Event(() => OkuboDonComot);
            Event(() => GrantItemsFaulted, x => x.CorrelateById(
                        context => context.Message.Message.CorrelationId
            ));
            Event(() => ComotOkuboFaulted, x => x.CorrelateById(
                context => context.Message.Message.CorrelationId
            ));
        }

        private void ConfigureInitialState()
        {
            Initially(
                When(PurchaseRequested)
                .Then(context =>
                {
                    context.Saga.UserId = context.Message.UserId;
                    context.Saga.ItemId = context.Message.ItemId;
                    context.Saga.Quantity = context.Message.Quantity;
                    context.Saga.Received = DateTimeOffset.UtcNow;
                    context.Saga.LastUpdated = context.Saga.Received;
                    logger.LogInformation("Calculating total price for purchase with Correlation ID:{CorrelationId}...", 
                    context.Saga.CorrelationId);
                })
                .Activity(x => x.OfType<CalculatePurchaseTotalActivity>())
                .Send((context => new GrantItems(
                    context.Saga.UserId,
                    context.Saga.ItemId,
                    context.Saga.Quantity,
                    context.Saga.CorrelationId)))
                .TransitionTo(Accepted)
                .Catch<Exception>(ex => ex
                    .Then(context =>
                    {
                        context.Saga.ErrorMessage = context.Exception.Message;
                        context.Saga.LastUpdated = DateTimeOffset.UtcNow;
                        logger.LogError(
                            context.Exception, 
                            "Error calculating total price for purchase with Correlation ID:{CorrelationId}. Error:{ErrorMessage}", 
                            context.Saga.CorrelationId,
                            context.Saga.ErrorMessage);
                    })
                    .TransitionTo(Faulted)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Saga))
                )

            );
        }

        private void ConfigureAccepted()
        {
            During(Accepted,
            Ignore(PurchaseRequested),
            When(InventoryItemsGranted)
                .Then(context =>
                {
                    context.Saga.LastUpdated = DateTimeOffset.Now;
                    logger.LogInformation("Items of purchase with Correlation ID:{CorrelationId} have been granted to User:{UserId}", 
                    context.Saga.CorrelationId,
                    context.Saga.UserId);
                })
                .Send(context => new ComotOkubo(
                    context.Saga.UserId,
                    context.Saga.PurchaseTotal.Value,
                    context.Saga.CorrelationId
                ))
                .TransitionTo(ItemsGranted),
            When(GrantItemsFaulted)
                .Then(context =>
                {
                    context.Saga.ErrorMessage = context.Message.Exceptions[0].Message;
                    context.Saga.LastUpdated = DateTimeOffset.UtcNow;
                    logger.LogError(
                            "Error granting items for purchase with Correlation ID:{CorrelationId}. Error:{ErrorMessage}", 
                            context.Saga.CorrelationId,
                            context.Saga.ErrorMessage);
                })
                .TransitionTo(Faulted)
                .ThenAsync(async context => await hub.SendStatusAsync(context.Saga))
            );
        }
        //Configuration runs at any state of the machine
        private void ConfigureAny()
        {
            DuringAny(
                When(GetPurchaseState)
                .Respond(x => x.Saga)
            );
        }

        private void ConfigureItemsGranted()
        {
            During(ItemsGranted,
                Ignore(PurchaseRequested),
                Ignore(InventoryItemsGranted),
                When(OkuboDonComot)
                    .Then(context =>
                    {
                        context.Saga.LastUpdated = DateTimeOffset.UtcNow;
                        logger.LogInformation(
                            "The total price of purchase with CorrelationId:{CorrelationId} don comot from User:{UserId}. Purchase complete",
                            context.Saga.CorrelationId,
                            context.Saga.UserId
                        );
                    })
                    .TransitionTo(Completed)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Saga)),
                When(ComotOkuboFaulted)
                    .Send(context => new SubtractItems(
                        context.Saga.UserId,
                        context.Saga.ItemId,
                        context.Saga.Quantity,
                        context.Saga.CorrelationId
                    ))
                    .Then(context =>
                    {
                        context.Saga.ErrorMessage = context.Message.Exceptions[0].Message;
                        context.Saga.LastUpdated = DateTimeOffset.UtcNow;
                        logger.LogError(
                            "Failed to comot the total price of purchase with CorrelationId:{CorrelationId} from User:{UserId}",
                            context.Saga.CorrelationId,
                            context.Saga.UserId);
                    })
                    .TransitionTo(Faulted)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Saga))
            );
        }

        private void ConfigureCompleted()
        {
            During(Completed,
                Ignore(PurchaseRequested),
                Ignore(InventoryItemsGranted),
                Ignore(OkuboDonComot)
            );
        }

        private void ConfigureFaulted()
        {
            During(Faulted,
                Ignore(PurchaseRequested),
                Ignore(InventoryItemsGranted),
                Ignore(OkuboDonComot)
            );
        }
    }
}