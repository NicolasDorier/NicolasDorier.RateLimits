[![NuGet](https://img.shields.io/nuget/v/NicolasDorier.RateLimits.svg)](https://www.nuget.org/packages/NicolasDorier.RateLimits)

# RateLimits library

## Introduction

This library is meant to allow you to control access to your resources or MVC routes based on the leaky bucket model of NGINX.

You can find more documentation on this abstraction on [this NGinx article](https://www.nginx.com/blog/rate-limiting-nginx/).

## Concept

You might want to limit access to your resources based on some logic.

The solution implemented by this repository is based on the same principles than [NGinx](https://www.nginx.com/blog/rate-limiting-nginx/).

Imagine that requests coming for access to your resource falls into a bucket.

The dimensions of your bucket are called `LimitRequestZone`.

If you want to create a hole in the bucket to be sure that only `10 requests per minutes` pass, your `LimitRequestZone` string would be:

```
zone=myzone rate=10r/m
```

If you apply such zone, as explained later, then only `1 request every 6 sec` (ie, the inverse of 10 requests per minutes) will be able to be serviced.

If 3 requests are sent in 6 seconds:

* The first requests will execute immediately
* The second will be dropped (typically, HTTP 429)
* The third will be dropped

What if you want `up to 3 requests to NOT be dropped`?

```
zone=myzone rate=10r/m burst=3
```

Now, if there is 4 requests in 6 seconds, 

* The first requests will execute immediately
* The second will be delayed until the 10 sec period is finished
* The third will be delayed until the second 10 sec period is finished
* The fourth will be dropped.

Now what if, you don't `do not want to delay` the execution, but still want to be sure about having in average 10 requests per min?

```
zone=myzone rate=10r/m burst=3 nodelay
```

If there is 4 requests in 6 seconds, 

* The first requests will execute immediately
* The second will execute immediately
* The third will execute immediately
* The fourth will be dropped

## How to use

### Setup

In your ASP.NET project, add reference to this library:

```bash
dotnet add package NicolasDorier.RateLimits
```

Then add the service in your `Startup.cs` class, for example:

```diff
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
+using NicolasDorier.RateLimits;

namespace MyAwesomeApp
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
+           services.AddRateLimits();
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc();
        }
    }
}
```

From now, you will be able to resolve the `RateLimitService` class to setup your `zones`.

Our goal will be to make sure that:
* a `specific IP` can't try to login to our website more than `10 times per min` (ie, 1 attempt every 6 seconds).
* We allow the user to bursts at most `3 attempts` per 6 seconds.
* We don't want to delay his requests (if the request can be treated, it should be done immedialely)

```csharp
public class ZoneLimits
{
    public const string Login = "login";
}
```
https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer
In your `Startup.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using NicolasDorier.RateLimits;

namespace MyAwesomeApp
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRateLimits();
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, RateLimitService rates)
        {
            // This make sure X-Forwarded-For is taken into account
            // See https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer
            var forwardingOptions = new ForwardedHeadersOptions();
            forwardingOptions.KnownNetworks.Clear();
            forwardingOptions.KnownProxies.Clear();
            forwardingOptions.ForwardedHeaders = ForwardedHeaders.All;
            app.UseForwardedHeaders(forwardingOptions);

+           rates.SetZone($"zone={ZoneLimits.Login} rate=10r/m burst=3 nodelay");
            app.UseMvc();
        }
    }
}
```

You then have two way of applying the rate limit to Login.

### Rate limits on MVC routes

The easiest is to apply `RateLimitsFilterAttribute` on your action.

```csharp
  [RateLimitsFilter(ZoneLimits.Login, Scope = RateLimitsScope.RemoteAddress)]
  public async Task<IActionResult> Login(string redirectUrl, LoginViewModel vm) 
  {
      ....
  }
```

`RateLimitsScope.RemoteAddress` will use the client's IP address to apply the limit. (Or from `X-Forwarded-For`/`X-Real-IP` header if present)

Available  scopes are:

* `RateLimitsScope.Global` limit rate globally.
* `RateLimitsScope.RemoteAddress` limit rate per IP.
* `RateLimitsScope.RouteData` limit rate per RouteData value data with key specified by `RateLimitsFilterAttribute.DataKey`
* `RateLimitsScope.ActionArgument` limit rate per Action Argument value with key specified by `RateLimitsFilterAttribute.DataKey`

The filter will throw HTTP error `429 Too Many Requests` if the user is sending too much requests.

### Rate limits in your own code

Imagine that you want to limit the rate from the login's username instead of IP address.

First, you resolve the `RateLimitService` in your constructor.
```csharp
RateLimitService rateLimit;
public MyController(RateLimitService rateLimit)
{
     this.rateLimit = rateLimit;
}
```
The you use it in your login with `RateLimitService.Throttle`:

```csharp
  public async Task<IActionResult> Login(string redirectUrl, LoginViewModel vm) 
  {
      if(!await rateLimit.Throttle(ZoneLimits.Login, vm.Username.Trim()))
        return new TooManyRequestsResult(ZoneLimits.Login);
      ....
  }
```

## License

This library is under the [MIT License](LICENSE).
