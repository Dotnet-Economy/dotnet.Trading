using System.Threading.Tasks;
using dotnet.Common;
using dotnet.Identity.Contracts;
using dotnet.Trading.Service.Entities;
using MassTransit;

namespace dotnet.Trading.Service.Consumers
{
    public class UserUpdatedConsumer : IConsumer<UserUpdated>
    {
        private readonly IRepository<ApplicationUser> repository;

        public UserUpdatedConsumer(IRepository<ApplicationUser> repository)
        {
            this.repository = repository;
        }

        public async Task Consume(ConsumeContext<UserUpdated> context)
        {
            var message = context.Message;
            var user = await repository.GetAsync(message.UserId);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    Id = message.UserId,
                    Okubo = message.NewTotalOkubo
                };

                await repository.CreateAsync(user);
            }
            else
            {
                user.Okubo = message.NewTotalOkubo;
                await repository.UpdateAsync(user);
            }
        }
    }
}