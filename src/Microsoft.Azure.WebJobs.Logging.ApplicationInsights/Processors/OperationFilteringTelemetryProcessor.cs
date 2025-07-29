// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Reflection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class OperationFilteringTelemetryProcessor : ITelemetryProcessor
    {
        private const string EventName = nameof(OperationFilteringTelemetryProcessor);
        private static readonly DiagnosticListener _source = new DiagnosticListener(string.Concat(ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix, nameof(OperationFilteringTelemetryProcessor)));

        private readonly ITelemetryProcessor _next;

        public OperationFilteringTelemetryProcessor(ITelemetryProcessor next)
        {
            _next = next;
        }

        public void Process(ITelemetry item)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new SafeContractResolver()
            };

            string act = "null";
            if (Activity.Current != null)
            {
                act = JsonConvert.SerializeObject(Activity.Current);
            }

            if (item is RequestTelemetry request)
            {
                _source.Write(EventName, "OperationFilteringTelemetryProcessor, found a request telemetry -" + item.GetType().Name + " - " + JsonConvert.SerializeObject(request, settings) + "------------" + act);
            }
            else
            {
                _source.Write(EventName, "OperationFilteringTelemetryProcessor Beginning-" + item.GetType().Name + " - " + JsonConvert.SerializeObject(item, settings) + "------------" + act);
            }
            // WebJobs host does many internal calls, polling queues and blobs, etc...
            // we do not want to report all of them by default, but only those which are relevant for
            // function execution: bindings and user code (which have category and level stamped on the telemetry).
            // So, if there is no category on the operation telemetry (request or dependency), we return.
            // This filter runs before QuickPulse to reduce logging internal 40x operations performed by the host.
            if (item is OperationTelemetry telemetry && !telemetry.Properties.ContainsKey(LogConstants.CategoryNameKey))
            {
                _source.Write(EventName, "OperationFilteringTelemetryProcessor Filtered out-" + item.GetType().Name + " - " + JsonConvert.SerializeObject(item, settings) + "------------" + act);
                return;
            }
            _source.Write(EventName, "OperationFilteringTelemetryProcessor End-" + item.GetType().Name);
            _next.Process(item);
        }
    }

    public class SafeContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
        {
            var prop = base.CreateProperty(member, serialization);

            if (prop.DeclaringType == typeof(RequestTelemetry) && prop.PropertyName == "HttpMethod")
            {
                prop.ShouldSerialize = _ => false;
            }

            return prop;
        }
    }
}
