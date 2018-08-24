using System;
using System.Collections.Generic;
using System.Text;

namespace NicolasDorier.RateLimits
{
    public enum RateLimitsScope
    {
        Global,
        RemoteAddress,
        RouteData,
        ActionArgument
    }
}
