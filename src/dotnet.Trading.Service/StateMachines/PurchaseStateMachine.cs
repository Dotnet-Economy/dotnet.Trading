using System;
using Automatonymous;
using dotnet.Identity.Contracts;
using dotnet.Inventory.Contracts;
using dotnet.Trading.Service.Activities;
using dotnet.Trading.Service.Contracts;
using dotnet.Trading.Service.SignalR;
using MassTransit;

namespace dotnet.Trading.Service.StateMachines
{
    public class PurchaseStateMachine : MassTransitStateMachine<PurchaseState>
    {

        private readonly MessageHub hub;
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

        public PurchaseStateMachine(MessageHub hub)
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
                    context.Instance.UserId = context.Data.UserId;
                    context.Instance.ItemId = context.Data.ItemId;
                    context.Instance.Quantity = context.Data.Quantity;
                    context.Instance.Received = DateTimeOffset.UtcNow;
                    context.Instance.LastUpdated = context.Instance.Received;
                })
                .Activity(x => x.OfType<CalculatePurchaseTotalActivity>())
                .Send((context => new GrantItems(
                    context.Instance.UserId,
                    context.Instance.ItemId,
                    context.Instance.Quantity,
                    context.Instance.CorrelationId)))
                .TransitionTo(Accepted)
                .Catch<Exception>(ex => ex
                    .Then(context =>
                    {
                        context.Instance.ErrorMessage = context.Exception.Message;
                        context.Instance.LastUpdated = DateTimeOffset.UtcNow;
                    })
                    .TransitionTo(Faulted)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Instance))
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
                    context.Instance.LastUpdated = DateTimeOffset.Now;
                })
                .Send(context => new ComotOkubo(
                    context.Instance.UserId,
                    context.Instance.PurchaseTotal.Value,
                    context.Instance.CorrelationId
                ))
                .TransitionTo(ItemsGranted),
            When(GrantItemsFaulted)
                .Then(context =>
                {
                    context.Instance.ErrorMessage = context.Data.Exceptions[0].Message;
                    context.Instance.LastUpdated = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Faulted)
                .ThenAsync(async context => await hub.SendStatusAsync(context.Instance))
            );
        }
        //Configuration runs at any state of the machine
        private void ConfigureAny()
        {
            DuringAny(
                When(GetPurchaseState)
                .Respond(x => x.Instance)
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
                        context.Instance.LastUpdated = DateTimeOffset.UtcNow;
                    })
                    .TransitionTo(Completed)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Instance)),
                When(ComotOkuboFaulted)
                    .Send(context => new SubtractItems(
                        context.Instance.UserId,
                        context.Instance.ItemId,
                        context.Instance.Quantity,
                        context.Instance.CorrelationId
                    ))
                    .Then(context =>
                    {
                        context.Instance.ErrorMessage = context.Data.Exceptions[0].Message;
                        context.Instance.LastUpdated = DateTimeOffset.UtcNow;
                    })
                    .TransitionTo(Faulted)
                    .ThenAsync(async context => await hub.SendStatusAsync(context.Instance))
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