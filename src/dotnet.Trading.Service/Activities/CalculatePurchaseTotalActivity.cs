using System;
using System.Threading.Tasks;
using Automatonymous;
using dotnet.Common;
using dotnet.Trading.Service.Contracts;
using dotnet.Trading.Service.Entities;
using dotnet.Trading.Service.Exceptions;
using dotnet.Trading.Service.StateMachines;
using GreenPipes;

namespace dotnet.Trading.Service.Activities
{
    public class CalculatePurchaseTotalActivity : Activity<PurchaseState, PurchaseRequested>
    {
        private readonly IRepository<CatalogItem> repository;

        public CalculatePurchaseTotalActivity(IRepository<CatalogItem> repository)
        {
            this.repository = repository;
        }

        public void Accept(StateMachineVisitor visitor)
        {
            visitor.Visit(this);
        }

        public async Task Execute(BehaviorContext<PurchaseState, PurchaseRequested> context, Behavior<PurchaseState, PurchaseRequested> next)
        {
            var message = context.Data;
            var item = await repository.GetAsync(message.ItemId);
            if (item == null) { throw new UnknownItemException(message.ItemId); }

            context.Instance.PurchaseTotal = item.Price * message.Quantity;
            context.Instance.LastUpdated = DateTimeOffset.UtcNow;

            await next.Execute(context).ConfigureAwait(false);
        }

        public Task Faulted<TException>(BehaviorExceptionContext<PurchaseState, PurchaseRequested, TException> context, Behavior<PurchaseState, PurchaseRequested> next) where TException : Exception
        {
            return next.Faulted(context);
        }

        public void Probe(ProbeContext context)
        {
            context.CreateScope("calculate-purchase-total");
        }
    }
}