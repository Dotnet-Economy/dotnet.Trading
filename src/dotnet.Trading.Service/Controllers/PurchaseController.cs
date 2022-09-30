using System;
using System.Security.Claims;
using System.Threading.Tasks;
using dotnet.Trading.Service.Contracts;
using dotnet.Trading.Service.Dtos;
using dotnet.Trading.Service.StateMachines;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dotnet.Trading.Service.Controllers
{
    [ApiController]
    [Route("purchase")]
    [Authorize]
    public class PurchaseController : ControllerBase
    {
        private readonly IPublishEndpoint publishEndpoint;
        private readonly IRequestClient<GetPurchaseState> purchaseClient;

        public PurchaseController(IPublishEndpoint publishEndpoint, IRequestClient<GetPurchaseState> purchaseClient)
        {
            this.publishEndpoint = publishEndpoint;
            this.purchaseClient = purchaseClient;
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync(SubmitPurchaseDto purchase)
        {
            var userId = User.FindFirstValue("sub");
            //TODO: Best practice?
            var correlationId = Guid.NewGuid();
            var message = new PurchaseRequested(
                Guid.Parse(userId),
                purchase.ItemId.Value,
                purchase.Quantity,
                correlationId
            );

            await publishEndpoint.Publish(message);

            return AcceptedAtAction(nameof(GetStatusAsync), new { correlationId }, new { correlationId });
        }

        [HttpGet("status/{correlationId}")]
        public async Task<ActionResult<PurchaseDto>> GetStatusAsync(Guid correlationId)
        {
            var response = await purchaseClient
            .GetResponse<PurchaseState>(new GetPurchaseState(correlationId));
            var purchaseState = response.Message;
            var purchase = new PurchaseDto(
                purchaseState.UserId,
                purchaseState.ItemId,
                purchaseState.PurchaseTotal,
                purchaseState.Quantity,
                purchaseState.CurrentState,
                purchaseState.ErrorMessage,
                purchaseState.Received,
                purchaseState.LastUpdated
            );

            return Ok(purchase);
        }
    }
}