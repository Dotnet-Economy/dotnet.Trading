using System;
using dotnet.Common;

namespace dotnet.Trading.Service.Entities
{
    public class InventoryItem : IEntity
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid CatalogItemId { get; set; }
        public int Quantity { get; set; }

    }
}