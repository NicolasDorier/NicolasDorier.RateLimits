using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace NicolasDorier.RateLimits.Tests
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().AddApplicationPart(typeof(Startup).Assembly);
            services.AddRateLimits();
        }

        public void Configure(IApplicationBuilder app, IRateLimitService service)
        {
            var forwardingOptions = new ForwardedHeadersOptions();
            forwardingOptions.KnownNetworks.Clear();
            forwardingOptions.KnownProxies.Clear();
            forwardingOptions.ForwardedHeaders = ForwardedHeaders.All;
            app.UseForwardedHeaders(forwardingOptions);
            service.SetZone("zone=mylimit rate=10r/s");
            app.UseMvc();
        }
    }
}
