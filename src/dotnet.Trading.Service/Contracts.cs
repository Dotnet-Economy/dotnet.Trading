using System;

namespace dotnet.Trading.Service.Contracts
{
    public record PurchaseRequested(
        Guid UserId,
        Guid ItemId,
        int Quantity,
        Guid CorrelationId
    );

    // TODO: Add event for querying current machine state
    public record GetPurchaseState(Guid CorrelationId);
}