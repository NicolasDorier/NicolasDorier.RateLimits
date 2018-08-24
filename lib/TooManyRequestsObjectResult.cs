using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace NicolasDorier.RateLimits
{
    /// <summary>
    /// An <see cref="ObjectResult"/> that when executed will produce a Not Found (404) response.
    /// </summary>
    public class TooManyRequestsObjectResult : ObjectResult
    {
        /// <summary>
        /// Creates a new <see cref="NotFoundObjectResult"/> instance.
        /// </summary>
        /// <param name="value">The value to format in the entity body.</param>
        public TooManyRequestsObjectResult(object value = null)
            : base(null)
        {
            StatusCode = 429;
        }
    }
}
