using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace NicolasDorier.RateLimits
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RateLimitsFilterAttribute : Attribute, IAsyncActionFilter
    {
        public RateLimitsFilterAttribute(string zoneName)
        {
            ZoneName = zoneName;
        }
        public string ZoneName
        {
            get; set;
        }

        public RateLimitsScope Scope
        {
            get; set;
        }

        public string DataKey
        {
            get; set;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var rateLimitService = context.HttpContext.RequestServices.GetService<IRateLimitService>();
            if(rateLimitService == null)
                throw new InvalidOperationException("Rate Limits not registered, please use service.AddRateLimits() in your Startup's ConfigureService method");

            if(!await Throttle(rateLimitService, context))
            {
                context.Result = new TooManyRequestsResult(ZoneName);
            }
            else
            {
                await next();
            }
        }

        private async Task<bool> Throttle(IRateLimitService rateLimitService, ActionExecutingContext context)
        {
            return await rateLimitService.Throttle(ZoneName, GetScope(context), context.HttpContext.RequestAborted);
        }

        private object GetScope(ActionExecutingContext context)
        {
            if(DataKey != null && !IsKeyableScope(Scope))
                throw new InvalidOperationException("A keyable scope should not be set if you do not use RateLimitsScope.DataKey");
            if(DataKey == null && IsKeyableScope(Scope))
                throw new InvalidOperationException("A keyable scope should be set if you use RateLimitsScope.DataKey");
            if(Scope == RateLimitsScope.Global)
                return null;
            else if(Scope == RateLimitsScope.RemoteAddress)
            {
                return context.HttpContext.Connection.RemoteIpAddress.ToString();
            }
            else if(IsKeyableScope(Scope))
            {
                IDictionary<string, object> kv = Scope == RateLimitsScope.RouteData ? context.RouteData.Values :
                                                 Scope == RateLimitsScope.ActionArgument ? context.ActionArguments :
                                                          throw new NotSupportedException();
                kv.TryGetValue(DataKey, out var v);
                return v;
            }
            else
                throw new NotSupportedException(Scope.ToString());
        }

        private bool IsKeyableScope(RateLimitsScope scope)
        {
            return scope == RateLimitsScope.ActionArgument ||
                   scope == RateLimitsScope.RouteData;
        }
    }
}
