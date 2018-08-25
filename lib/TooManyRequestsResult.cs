using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace NicolasDorier.RateLimits
{
    /// <summary>
    /// An <see cref="ObjectResult"/> that when executed will produce a Not Found (429) response.
    /// </summary>
    public class TooManyRequestsResult : StatusCodeResult
    {
        /// <summary>
        /// Creates a new <see cref="NotFoundObjectResult"/> instance.
        /// </summary>
        /// <param name="value">The value to format in the entity body.</param>
        public TooManyRequestsResult(string zoneName)
            : base(429)
        {
            ZoneName = zoneName;
        }

        public string ZoneName { get; }
    }
}
