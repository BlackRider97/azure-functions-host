// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Azure.WebJobs.Script.Workers.Rpc;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Script.Config
{
    internal class ScriptTelemetryProcessor : ITelemetryProcessor
    {
        private const string EventName = nameof(ScriptTelemetryProcessor);
        private static readonly DiagnosticListener _source = new DiagnosticListener(string.Concat(ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix, nameof(ScriptTelemetryProcessor)));

        public ScriptTelemetryProcessor(ITelemetryProcessor next)
        {
            this.Next = next;
        }

        private ITelemetryProcessor Next { get; set; }

        public void Process(ITelemetry item)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new SafeContractResolver()
            };

            _source.Write(EventName, "ScriptTelemetryProcessor Beginning-" + JsonConvert.SerializeObject(item, settings));
            // Only process if exception is thrown by user code (if IsUserException is true).
            if (item is ExceptionTelemetry exceptionTelemetry
                && exceptionTelemetry?.Exception?.InnerException is RpcException rpcException
                && (rpcException?.IsUserException).GetValueOrDefault())
            {
                item = ToUserException(rpcException, item);
            }
            _source.Write(EventName, "ScriptTelemetryProcessor End-" + JsonConvert.SerializeObject(item, settings));
            this.Next.Process(item);
        }

        private ITelemetry ToUserException(RpcException rpcException, ITelemetry originalItem)
        {
            string typeName = string.IsNullOrEmpty(rpcException.RemoteTypeName) ? rpcException.GetType().ToString() : rpcException.RemoteTypeName;

            var userExceptionDetails = new ExceptionDetailsInfo(1, -1, typeName, rpcException.RemoteMessage, true, rpcException.RemoteStackTrace, new ApplicationInsights.DataContracts.StackFrame[] { });

            ExceptionTelemetry newET = new ExceptionTelemetry(new[] { userExceptionDetails },
            SeverityLevel.Error, "ProblemId",
            new Dictionary<string, string>() { },
            new Dictionary<string, double>() { });

            newET.Context.InstrumentationKey = originalItem.Context.InstrumentationKey;
            newET.Timestamp = originalItem.Timestamp;

            return newET;
        }

        /// <summary>
        /// Returns true if the feature flag for surfacing user code exceptions was set by the worker,
        /// and false if not.
        /// </summary>
        /// <param name="ex">The <see cref="Exception"/> instance.</param>
        private bool EnableUserExceptionFeatureFlag(Exception ex)
        {
            try
            {
                string value = (string)ex.Data[RpcWorkerConstants.EnableUserCodeException];
                return bool.Parse(value);
            }
            catch
            {
                return false;
            }
        }
    }
}
