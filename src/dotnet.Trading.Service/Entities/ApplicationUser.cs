using System;
using dotnet.Common;

namespace dotnet.Trading.Service.Entities
{
    public class ApplicationUser : IEntity
    {
        public Guid Id { get; set; }
        public decimal Okubo { get; set; }
    }
}