using System.Threading.Tasks;
using dotnet.Catalog.Contracts;
using dotnet.Common;
using dotnet.Trading.Service.Entities;
using MassTransit;

namespace dotnet.Trading.Service.Consumers
{
    public class CatalogItemCreatedConsumer : IConsumer<CatalogItemCreated>
    {
        public readonly IRepository<CatalogItem> repository;

        public CatalogItemCreatedConsumer(IRepository<CatalogItem> repository)
        {
            this.repository = repository;
        }
        public async Task Consume(ConsumeContext<CatalogItemCreated> context)
        {
            var message = context.Message;
            var item = await repository.GetAsync(message.ItemId);
            if (item != null) return;
            item = new CatalogItem
            {
                Id = message.ItemId,
                Name = message.Name,
                Description = message.Description,
                Price = message.Price
            };

            await repository.CreateAsync(item);
        }
    }
}