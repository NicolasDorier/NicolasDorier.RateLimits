using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace NicolasDorier.RateLimits.Tests.Controllers
{
    [Route("/")]
    public class TestController : Controller
    {
        [HttpGet()]
        [Route(Limits.Global)]
        [RateLimitsFilter(Limits.Global)]
        public IActionResult Global()
        {
            return Ok();
        }

        [HttpGet()]
        public IActionResult Index(int id)
        {
            return Ok();
        }

        [HttpGet()]
        [Route(Limits.IpBased)]
        [RateLimitsFilter(Limits.IpBased, Scope = RateLimitsScope.RemoteAddress)]
        public IActionResult Ip()
        {
            return Ok();
        }

        [HttpGet(Limits.ActionArgument)]
        [Route(Limits.ActionArgument)]
        [RateLimitsFilter(Limits.ActionArgument, Scope = RateLimitsScope.ActionArgument, DataKey = "somevalue")]
        public IActionResult Route(string somevalue)
        {
            return Ok();
        }
    }
}
