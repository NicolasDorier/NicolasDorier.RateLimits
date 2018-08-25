using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            var rateLimitService = context.HttpContext.RequestServices.GetService(typeof(RateLimitService)) as RateLimitService;
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

        private async Task<bool> Throttle(RateLimitService rateLimitService, ActionExecutingContext context)
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
                var ip = context.HttpContext.Connection.RemoteIpAddress.ToString();
                if(context.HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var stringValues))
                {
                    var newIp = stringValues.FirstOrDefault();
                    if(newIp != null)
                    {
                        ip = newIp;
                    }
                }
                if(context.HttpContext.Request.Headers.TryGetValue("X-Real-Ip", out stringValues))
                {
                    var newIp = stringValues.FirstOrDefault();
                    if(newIp != null)
                    {
                        ip = newIp;
                    }
                }
                return ip;
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
