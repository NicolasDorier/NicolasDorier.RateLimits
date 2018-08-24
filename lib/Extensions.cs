using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NicolasDorier.RateLimits
{
    public static class Extensions
    {
        public static IServiceCollection AddRateLimits(this IServiceCollection services)
        {
            var instance = new RateLimitService();
            services.TryAddSingleton(instance);
            return services;
        }
    }
}
