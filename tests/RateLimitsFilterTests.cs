using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using NicolasDorier.RateLimits;
using Microsoft.AspNetCore.Builder;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using Xunit;
using System.Threading;

namespace NicolasDorier.RateLimits.Tests
{
    public class RateLimitsFilterTests
    {
        public class Query
        {
            private readonly HttpClient client;
            private readonly string address;

            public Query(HttpClient client, string address)
            {
                this.client = client;
                this.address = address;
            }

            internal void AssertOk()
            {
                Assert.True(client.GetAsync(address).Result.IsSuccessStatusCode);
            }

            internal void AssertExceedLimits()
            {
                Assert.Equal(429, (int)client.GetAsync(address).Result.StatusCode);
            }
        }
        public class Tester : IDisposable
        {
            public static int FreeTcpPort()
            {
                TcpListener l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                int port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }
            public Tester()
            {
                var port = FreeTcpPort();
                Host = new WebHostBuilder().UseKestrel(opt =>
                {
                    opt.ListenLocalhost(port);
                }).UseStartup<Startup>().Build();
                Host.Start();
                RateLimitService = (RateLimitService)Host.Services.GetService(typeof(RateLimitService));
                Client = new HttpClient();
                Client.BaseAddress = new Uri("http://localhost:" + port);
            }

            public RateLimitService RateLimitService
            {
                get; set;
            }

            public IWebHost Host
            {
                get; set;
            }
            public HttpClient Client
            {
                get; set;
            }

            public static Tester Create()
            {
                return new Tester();
            }

            public void Dispose()
            {
                Host.Dispose();
            }

            public Query Query(string address)
            {
                return new Query(Client, address);
            }
        }



        [Fact]
        public void CanFilterBasedGlobalContext()
        {
            using(var tester = Tester.Create())
            {
                tester.RateLimitService.SetZone($"zone={Limits.Global} rate=1r/s");
                tester.Query(Limits.Global).AssertOk();
                tester.Query(Limits.Global).AssertExceedLimits();
                Thread.Sleep(1100);
                tester.Query(Limits.Global).AssertOk();
                tester.Query(Limits.Global).AssertExceedLimits();
            }
        }

        [Fact]
        public void CanFilterBasedIPContext()
        {
            using(var tester = Tester.Create())
            {
                tester.RateLimitService.SetZone($"zone={Limits.IpBased} rate=1r/s");
                tester.Query(Limits.IpBased).AssertOk();
                tester.Query(Limits.IpBased).AssertExceedLimits();
                Thread.Sleep(1100);
                tester.Query(Limits.IpBased).AssertOk();
                tester.Query(Limits.IpBased).AssertExceedLimits();
                tester.Client.DefaultRequestHeaders.Add("X-Real-IP", "13.32.31.2");
                tester.Query(Limits.IpBased).AssertOk();
                tester.Query(Limits.IpBased).AssertExceedLimits();
                tester.Client.DefaultRequestHeaders.Remove("X-Real-IP");
                tester.Client.DefaultRequestHeaders.Add("X-Forwarded-For", "13.32.31.2");
                tester.Query(Limits.IpBased).AssertExceedLimits();
            }
        }

        [Fact]
        public void CanFilterBasedOnActionArg()
        {
            using(var tester = Tester.Create())
            {
                tester.RateLimitService.SetZone($"zone={Limits.ActionArgument} rate=1r/s");
                tester.Query(Limits.ActionArgument).AssertOk();
                tester.Query(Limits.ActionArgument).AssertExceedLimits();
                Thread.Sleep(1100);
                tester.Query(Limits.ActionArgument).AssertOk();
                tester.Query(Limits.ActionArgument).AssertExceedLimits();
                tester.Query(Limits.ActionArgument + "?somevalue=1").AssertOk();
                tester.Query(Limits.ActionArgument + "?somevalue=1").AssertExceedLimits();
                tester.Query(Limits.ActionArgument + "?somevalue=2").AssertOk();
                tester.Query(Limits.ActionArgument + "?somevalue=2").AssertExceedLimits();
            }
        }
    }
}
