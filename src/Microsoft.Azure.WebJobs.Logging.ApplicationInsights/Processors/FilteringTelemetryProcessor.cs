// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class FilteringTelemetryProcessor : ITelemetryProcessor
    {
        private static readonly LoggerRuleSelector RuleSelector = new LoggerRuleSelector();
        private static readonly Type ProviderType = typeof(ApplicationInsightsLoggerProvider);
        private const string EventName = nameof(FilteringTelemetryProcessor);
        private static readonly DiagnosticListener _source = new DiagnosticListener(string.Concat(ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix, nameof(FilteringTelemetryProcessor)));


        private readonly ConcurrentDictionary<string, LoggerFilterRule> _ruleMap = new ConcurrentDictionary<string, LoggerFilterRule>();
        private readonly LoggerFilterOptions _filterOptions;
        private ITelemetryProcessor _next;

        public FilteringTelemetryProcessor(LoggerFilterOptions filterOptions, ITelemetryProcessor next)
        {
            _filterOptions = filterOptions;
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
                _source.Write(EventName, "FilteringTelemetryProcessor, found a request telemetry -" + item.GetType().Name + " - " + JsonConvert.SerializeObject(request, settings) + "------------" + act);
            }
            else
            {
                _source.Write(EventName, "FilteringTelemetryProcessor Beginning-" + item.GetType().Name + " - " + JsonConvert.SerializeObject(item, settings) + "------------" + act);
            }

            if (IsEnabled(item))
            {
                _source.Write(EventName, "FilteringTelemetryProcessor, allowed -" + item.GetType().Name + " - " + JsonConvert.SerializeObject(item, settings) + "------------" + act);
                _next.Process(item);
            }
        }

        private bool IsEnabled(ITelemetry item)
        {
            bool enabled = true;

            if (item is ISupportProperties telemetry && _filterOptions != null)
            {
                if (!telemetry.Properties.TryGetValue(LogConstants.CategoryNameKey, out string categoryName))
                {
                    // If no category is specified, it will be filtered by the default filter
                    categoryName = string.Empty;
                }

                // Extract the log level and apply the filter
                if (telemetry.Properties.TryGetValue(LogConstants.LogLevelKey, out string logLevelString) &&
                    LogLevelExtension.TryParseOptimized(logLevelString, out LogLevel logLevel))
                {
                    LoggerFilterRule filterRule = _ruleMap.GetOrAdd(categoryName, c => SelectRule(c));

                    if (filterRule.LogLevel != null && logLevel < filterRule.LogLevel)
                    {
                        enabled = false;
                    }
                    else if (filterRule.Filter != null)
                    {
                        enabled = filterRule.Filter(ProviderType.FullName, categoryName, logLevel);
                    }
                }
            }

            return enabled;
        }

        private LoggerFilterRule SelectRule(string categoryName)
        {
            RuleSelector.Select(_filterOptions, ProviderType, categoryName,
                out LogLevel? minLevel, out Func<string, string, LogLevel, bool> filter);

            return new LoggerFilterRule(ProviderType.FullName, categoryName, minLevel, filter);
        }
    }
}
