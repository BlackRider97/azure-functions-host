// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Script.Config
{
    internal class ScriptTelemetryInitializer : ITelemetryInitializer
    {
        private const string EventName = nameof(ScriptTelemetryInitializer);
        private static readonly DiagnosticListener _source = new DiagnosticListener(string.Concat(ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix, nameof(ScriptTelemetryInitializer)));

        private readonly ScriptJobHostOptions _hostOptions;

        public ScriptTelemetryInitializer(IOptions<ScriptJobHostOptions> hostOptions)
        {
            if (hostOptions == null)
            {
                _source.Write(EventName, new { Message = "ScriptTelemetryInitializer: hostOptions is null" });
                throw new ArgumentNullException(nameof(hostOptions));
            }

            if (hostOptions.Value == null)
            {
                _source.Write(EventName, new { Message = "ScriptTelemetryInitializer: hostOptions.Value is null" });
                throw new ArgumentNullException(nameof(hostOptions.Value));
            }

            _hostOptions = hostOptions.Value;
        }

        public void Initialize(ITelemetry telemetry)
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

            _source.Write(EventName, "ScriptTelemetryInitializer Beginning-" + telemetry.GetType().Name + " - " + JsonConvert.SerializeObject(telemetry, settings) + "------------" + act);

            IDictionary<string, string> telemetryProps = telemetry?.Context?.Properties;

            if (telemetryProps == null)
            {
                return;
            }

            telemetryProps[ScriptConstants.LogPropertyHostInstanceIdKey] = _hostOptions.InstanceId;
            _source.Write(EventName, "ScriptTelemetryInitializer End-" + telemetry.GetType().Name + " - " + JsonConvert.SerializeObject(telemetry, settings) + "------------" + act);
        }
    }
}
