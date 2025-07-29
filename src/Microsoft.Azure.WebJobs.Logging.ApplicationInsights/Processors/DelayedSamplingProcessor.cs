// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class DelayedSamplingProcessor : ITelemetryProcessor
    {
        private const string EventName = nameof(DelayedSamplingProcessor);
        private readonly AdaptiveSamplingTelemetryProcessor _samplingProcessor;
        private static readonly DiagnosticListener _source = new DiagnosticListener(string.Concat(ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix, nameof(DelayedSamplingProcessor)));

        private ITelemetryProcessor _next;
        private bool _isSamplingEnabled = false;

        public DelayedSamplingProcessor(ITelemetryProcessor next, ApplicationInsightsLoggerOptions options)
        {
            _next = next;
            _samplingProcessor = TelemetryProcessorFactory.CreateAdaptiveSamplingProcessor(options, next);

            // Start a timer to enable sampling after a delay
            Task.Delay(options.AdaptiveSamplingInitializationDelay).ContinueWith(t => EnableSampling());
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
                _source.Write(EventName, "DelayedSamplingProcessor, found a request telemetry -" + item.GetType().Name + " - " + JsonConvert.SerializeObject(request, settings) + "------------" + act);
            }

            if (_isSamplingEnabled)
            {
                // Forward to Adaptive Sampling processor
                _samplingProcessor.Process(item);
                _source.Write(EventName, "DelayedSamplingProcessor - Forward" + item.GetType().Name + " - " + JsonConvert.SerializeObject(item, settings) + "------------" + act);
            }
            else
            {
                // Bypass sampling
                _source.Write(EventName, "DelayedSamplingProcessor - Bypass" + item.GetType().Name + " - " + JsonConvert.SerializeObject(item, settings) + "------------" + act);
                _next.Process(item);
            }
        }

        private void EnableSampling()
        {
            _isSamplingEnabled = true;
        }
    }
}
