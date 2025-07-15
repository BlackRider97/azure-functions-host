// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using static Microsoft.ApplicationInsights.SnapshotCollector.SnapshotCollectorDiagnosticSource;

namespace Microsoft.Azure.WebJobs.Script.Config
{
    internal class WorkerTraceFilterTelemetryProcessor : ITelemetryProcessor
    {
        private const string EventName = nameof(WorkerTraceFilterTelemetryProcessor);
        private readonly ITelemetryProcessor _next;
        private static readonly DiagnosticListener _source = new DiagnosticListener(string.Concat(ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix, nameof(WorkerTraceFilterTelemetryProcessor)));

        internal static readonly AsyncLocal<bool> FilterApplicationInsightsFromWorker = new();

        public WorkerTraceFilterTelemetryProcessor(ITelemetryProcessor next)
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
            _source.Write(EventName, "WorkerTraceFilterTelemetryProcessor Beginning Activity.Current- " + act);

            if (item is RequestTelemetry request)
            {
                _source.Write(EventName, "WorkerTraceFilterTelemetryProcessor Beginning-, found a request telemetry -" + JsonConvert.SerializeObject(request, settings));
            }
            else
            {
                _source.Write(EventName, "WorkerTraceFilterTelemetryProcessor Beginning-" + JsonConvert.SerializeObject(item, settings));
            }

            if (FilterApplicationInsightsFromWorker.Value)
            {
                _source.Write(EventName, "WorkerTraceFilterTelemetryProcessor Filtered out -" + JsonConvert.SerializeObject(item, settings));
                return;
            }
            _source.Write(EventName, "WorkerTraceFilterTelemetryProcessor End-" + JsonConvert.SerializeObject(item, settings));
            _next.Process(item);
        }
    }

    internal class LastProcessor : ITelemetryProcessor
    {
        private const string EventName = nameof(LastProcessor);
        private readonly ITelemetryProcessor _next;
        private static readonly DiagnosticListener _source = new DiagnosticListener(string.Concat(ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix, nameof(LastProcessor)));

        internal static readonly AsyncLocal<bool> FilterApplicationInsightsFromWorker = new();

        public LastProcessor(ITelemetryProcessor next)
        {
            _next = next;
        }

        public void Process(ITelemetry item)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new SafeContractResolver()
            };

            if (item is RequestTelemetry request)
            {
                _source.Write(EventName, "LastProcessor, found a request telemetry -" + JsonConvert.SerializeObject(request, settings));
            }
            else
            {
                _source.Write(EventName, "LastProcessor -" + JsonConvert.SerializeObject(item, settings));
            }
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
