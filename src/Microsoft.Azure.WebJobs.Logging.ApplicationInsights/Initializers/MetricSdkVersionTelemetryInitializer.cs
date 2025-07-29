// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class MetricSdkVersionTelemetryInitializer : ITelemetryInitializer
    {
        private const string Prefix = "af_";
        private const string EventName = nameof(MetricSdkVersionTelemetryInitializer);
        private static readonly DiagnosticListener _source = new DiagnosticListener(string.Concat(ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix, nameof(MetricSdkVersionTelemetryInitializer)));

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new SafeContractResolver()
            };

            string act = "null";
            if (Activity.Current != null)
            {
                act = JsonConvert.SerializeObject(Activity.Current);
            }
            _source.Write(EventName, "MetricSdkVersionTelemetryInitializer Beginning-" + telemetry.GetType().Name + " - " + JsonConvert.SerializeObject(telemetry, settings) + "------------" + act);

            if (telemetry is MetricTelemetry)
            {
                var internalContext = telemetry.Context?.GetInternalContext();
                if (internalContext != null && internalContext.SdkVersion != null && !internalContext.SdkVersion.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    internalContext.SdkVersion = Prefix + internalContext.SdkVersion;
                }
            }
            _source.Write(EventName, "MetricSdkVersionTelemetryInitializer End-" + telemetry.GetType().Name + " - " + JsonConvert.SerializeObject(telemetry, settings) + "------------" + act);
        }
    }
}