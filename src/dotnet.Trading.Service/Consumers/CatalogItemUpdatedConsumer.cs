using System.Threading.Tasks;
using dotnet.Catalog.Contracts;
using dotnet.Common;
using dotnet.Trading.Service.Entities;
using MassTransit;

namespace dotnet.Trading.Service.Consumers
{
    public class CatalogItemUpdatedConsumer : IConsumer<CatalogItemUpdated>
    {
        private readonly IRepository<CatalogItem> repository;

        public CatalogItemUpdatedConsumer(IRepository<CatalogItem> repository)
        {
            this.repository = repository;
        }
        public async Task Consume(ConsumeContext<CatalogItemUpdated> context)
        {
            var message = context.Message;
            var item = await repository.GetAsync(message.ItemId);
            if (item == null)
            {
                item = new CatalogItem
                {
                    Id = message.ItemId,
                    Name = message.Name,
                    Description = message.Description,
                    Price = message.Price
                };

                await repository.CreateAsync(item);
            }
            else
            {
                item.Name = message.Name;
                item.Description = message.Description;
                item.Price = message.Price;
                await repository.UpdateAsync(item);
            }


        }
    }
}