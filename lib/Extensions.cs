using Microsoft.Extensions.DependencyInjection;
using NicolasDorier.RateLimits;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class RateLimitsExtensions
    {
        public static IServiceCollection AddRateLimits(this IServiceCollection services)
        {
            var instance = new RateLimitService();
            services.AddSingleton(instance);
            return services;
        }
    }
}
