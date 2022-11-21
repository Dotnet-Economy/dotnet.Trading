using System;
using System.Security.Claims;
using System.Threading.Tasks;
using dotnet.Trading.Service.Contracts;
using dotnet.Trading.Service.Dtos;
using dotnet.Trading.Service.StateMachines;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace dotnet.Trading.Service.Controllers
{
    [ApiController]
    [Route("purchase")]
    [Authorize]
    public class PurchaseController : ControllerBase
    {
        private readonly IPublishEndpoint publishEndpoint;
        private readonly IRequestClient<GetPurchaseState> purchaseClient;
        private readonly ILogger<PurchaseController> logger;

        public PurchaseController(IPublishEndpoint publishEndpoint, IRequestClient<GetPurchaseState> purchaseClient, ILogger<PurchaseController> logger)
        {
            this.publishEndpoint = publishEndpoint;
            this.purchaseClient = purchaseClient;
            this.logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync(SubmitPurchaseDto purchase)
        {
            var userId = User.FindFirstValue("sub");

            logger.LogInformation("Received purchase request of {Quantity} of item {ItemId} from user {UserId}. Correlation ID:{CorrelationId}",
            purchase.Quantity,
            purchase.ItemId,
            userId,
            purchase.IdempotencyId);

            var message = new PurchaseRequested(
                Guid.Parse(userId),
                purchase.ItemId.Value,
                purchase.Quantity,
                purchase.IdempotencyId.Value
            );

            await publishEndpoint.Publish(message);

            return AcceptedAtAction(nameof(GetStatusAsync), new { purchase.IdempotencyId }, new { purchase.IdempotencyId });
        }

        [HttpGet("status/{idempotencyId}")]
        public async Task<ActionResult<PurchaseDto>> GetStatusAsync(Guid idempotencyId)
        {
            var response = await purchaseClient
            .GetResponse<PurchaseState>(new GetPurchaseState(idempotencyId));
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