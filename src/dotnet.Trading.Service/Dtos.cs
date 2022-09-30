using System;
using System.ComponentModel.DataAnnotations;

namespace dotnet.Trading.Service.Dtos
{
    public record SubmitPurchaseDto(
        [Required] Guid? ItemId,
        [Range(1, 100)] int Quantity
    );

    public record PurchaseDto(
        Guid UserId,
        Guid ItemId,
        decimal? PurchaseTotal,
        int Quantity,
        string State,
        string Reason,
        DateTimeOffset Received,
        DateTimeOffset LastUpdated
    );
}