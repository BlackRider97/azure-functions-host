// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Script;
using Microsoft.Azure.WebJobs.Script.Description;
using Yarp.ReverseProxy.Forwarder;

public class ScriptInvocationRequestTransformer : HttpTransformer
{
    public override async ValueTask TransformRequestAsync(HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

        if (httpContext.Items.TryGetValue(ScriptConstants.HttpProxyScriptInvocationContext, out object result)
            && result is ScriptInvocationContext scriptContext)
        {
            proxyRequest.Options.TryAdd(ScriptConstants.HttpProxyScriptInvocationContext, scriptContext);
        }
    }
}