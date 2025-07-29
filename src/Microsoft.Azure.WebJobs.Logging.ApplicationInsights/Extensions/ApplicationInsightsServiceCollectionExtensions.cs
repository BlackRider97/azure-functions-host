// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Text;
using Azure.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.AspNetCore;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.AspNetCore.TelemetryInitializers;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.Extensibility.Implementation.ApplicationId;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    internal static class ApplicationInsightsServiceCollectionExtensions
    {
        private const string EventName = nameof(ApplicationInsightsServiceCollectionExtensions);
        private static readonly DiagnosticListener _source = new DiagnosticListener(string.Concat(ApplicationInsightsDiagnosticConstants.ApplicationInsightsDiagnosticSourcePrefix, nameof(ApplicationInsightsServiceCollectionExtensions)));

        public static IServiceCollection AddApplicationInsights(this IServiceCollection services)
        {
            return services.AddApplicationInsights(_ => { }, _ => { });
        }

        public static IServiceCollection AddApplicationInsights(this IServiceCollection services,
            Action<ApplicationInsightsLoggerOptions> loggerOptionsConfiguration)
        {
            services.AddApplicationInsights(loggerOptionsConfiguration, _ => { });
            return services;
        }

        internal static IServiceCollection AddApplicationInsights(this IServiceCollection services,
            Action<ApplicationInsightsLoggerOptions> loggerOptionsConfiguration,
            Action<TelemetryConfiguration> additionalTelemetryConfig)
        {
            services.TryAddSingleton<ISdkVersionProvider, WebJobsSdkVersionProvider>();
            services.TryAddSingleton<IRoleInstanceProvider, WebJobsRoleInstanceProvider>();

            // Bind to the configuration section registered with
            services.AddOptions<ApplicationInsightsLoggerOptions>()
                .Configure<ILoggerProviderConfiguration<ApplicationInsightsLoggerProvider>>((options, config) =>
                {
                    config.Configuration?.Bind(options);
                });

            services.AddSingleton<ITelemetryInitializer, HttpDependenciesParsingTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection)
                {
                    var httpContextAccessor = provider.GetService<IHttpContextAccessor>();
                    if (httpContextAccessor != null)
                    {
                        return new ClientIpHeaderTelemetryInitializer(httpContextAccessor);
                    }
                }

                return NullTelemetryInitializer.Instance;
            });

            services.AddSingleton<ITelemetryInitializer, WebJobsRoleEnvironmentTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, WebJobsTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, MetricSdkVersionTelemetryInitializer>();
            services.AddSingleton<QuickPulseInitializationScheduler>();
            services.AddSingleton<QuickPulseTelemetryModule>();

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.EnableLiveMetrics)
                {
                    return provider.GetService<QuickPulseTelemetryModule>();
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.EnablePerformanceCountersCollection)
                {
                    return new PerformanceCollectorModule
                    {
                        // Disabling this can improve cold start times
                        EnableIISExpressPerformanceCounters = false
                    };
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.DiagnosticsEventListenerLogLevel != null)
                {
                    return new SelfDiagnosticsTelemetryModule((EventLevel)options.DiagnosticsEventListenerLogLevel);
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<IApplicationIdProvider, ApplicationInsightsApplicationIdProvider>();

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                var options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;

                DependencyTrackingTelemetryModule dependencyCollector = null;
                if (options.EnableDependencyTracking)
                {
                    dependencyCollector = new DependencyTrackingTelemetryModule();
                    var excludedDomains = dependencyCollector.ExcludeComponentCorrelationHttpHeadersOnDomains;
                    excludedDomains.Add("core.windows.net");
                    excludedDomains.Add("core.chinacloudapi.cn");
                    excludedDomains.Add("core.cloudapi.de");
                    excludedDomains.Add("core.usgovcloudapi.net");
                    excludedDomains.Add("localhost");
                    excludedDomains.Add("127.0.0.1");

                    var includedActivities = dependencyCollector.IncludeDiagnosticSourceActivities;
                    includedActivities.Add("Microsoft.Azure.ServiceBus");
                    includedActivities.Add("Microsoft.Azure.EventHubs");

                    if (options.DependencyTrackingOptions != null)
                    {
                        dependencyCollector.DisableRuntimeInstrumentation = options.DependencyTrackingOptions.DisableRuntimeInstrumentation;
                        dependencyCollector.DisableDiagnosticSourceInstrumentation = options.DependencyTrackingOptions.DisableDiagnosticSourceInstrumentation;
                        dependencyCollector.EnableLegacyCorrelationHeadersInjection = options.DependencyTrackingOptions.EnableLegacyCorrelationHeadersInjection;
                        dependencyCollector.EnableRequestIdHeaderInjectionInW3CMode = options.DependencyTrackingOptions.EnableRequestIdHeaderInjectionInW3CMode;
                        dependencyCollector.EnableSqlCommandTextInstrumentation = options.DependencyTrackingOptions.EnableSqlCommandTextInstrumentation;
                        dependencyCollector.SetComponentCorrelationHttpHeaders = options.DependencyTrackingOptions.SetComponentCorrelationHttpHeaders;
                        dependencyCollector.EnableAzureSdkTelemetryListener = options.DependencyTrackingOptions.EnableAzureSdkTelemetryListener;
                    }

                    return dependencyCollector;
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<ITelemetryModule>(provider =>
            {
                var options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;
                if (options.HttpAutoCollectionOptions.EnableHttpTriggerExtendedInfoCollection)
                {
                    var appIdProvider = provider.GetService<IApplicationIdProvider>();

                    return new RequestTrackingTelemetryModule(appIdProvider)
                    {
                        CollectionOptions = new RequestCollectionOptions
                        {
                            TrackExceptions = false, // webjobs/functions track exceptions themselves
                            InjectResponseHeaders = options.HttpAutoCollectionOptions.EnableResponseHeaderInjection
                        }
                    };
                }

                return NullTelemetryModule.Instance;
            });

            services.AddSingleton<ITelemetryModule, AppServicesHeartbeatTelemetryModule>();

            services.AddSingleton<ITelemetryChannel, ServerTelemetryChannel>();
            services.AddSingleton<TelemetryConfiguration>(provider =>
            {
                ApplicationInsightsLoggerOptions options = provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>().Value;

                Activity.DefaultIdFormat = options.HttpAutoCollectionOptions.EnableW3CDistributedTracing
                    ? ActivityIdFormat.W3C
                    : ActivityIdFormat.Hierarchical;
                Activity.ForceDefaultIdFormat = true;

                // If we do not want to filter LiveMetrics logs, we need to "late filter" using the
                // custom filter options that were passed in during initialization.
                LoggerFilterOptions filterOptions = null;
                if (options.EnableLiveMetrics && !options.EnableLiveMetricsFilters)
                {
                    filterOptions = CreateFilterOptions(provider.GetService<IOptions<LoggerFilterOptions>>().Value);
                }

                ITelemetryChannel channel = provider.GetService<ITelemetryChannel>();
                TelemetryConfiguration config = TelemetryConfiguration.CreateDefault();

                IApplicationIdProvider appIdProvider = provider.GetService<IApplicationIdProvider>();
                ISdkVersionProvider sdkVersionProvider = provider.GetService<ISdkVersionProvider>();
                IRoleInstanceProvider roleInstanceProvider = provider.GetService<IRoleInstanceProvider>();

                // Because of https://github.com/Microsoft/ApplicationInsights-dotnet-server/issues/943
                // we have to touch (and create) Active configuration before initializing telemetry modules
                // Active configuration is used to report AppInsights heartbeats
                // role environment telemetry initializer is needed to correlate heartbeats to particular host

                var activeConfig = TelemetryConfiguration.Active;
                if (!string.IsNullOrEmpty(options.InstrumentationKey) &&
                    string.IsNullOrEmpty(activeConfig.InstrumentationKey))
                {
                    activeConfig.InstrumentationKey = options.InstrumentationKey;
                }

                // Set ConnectionString second because it takes precedence and
                // we don't want InstrumentationKey to overwrite the value
                // ConnectionString sets
                if (!string.IsNullOrEmpty(options.ConnectionString) &&
                    string.IsNullOrEmpty(activeConfig.ConnectionString))
                {
                    activeConfig.ConnectionString = options.ConnectionString;
                }

                if (!activeConfig.TelemetryInitializers.OfType<WebJobsRoleEnvironmentTelemetryInitializer>().Any())
                {
                    activeConfig.TelemetryInitializers.Add(new WebJobsRoleEnvironmentTelemetryInitializer());
                    activeConfig.TelemetryInitializers.Add(new WebJobsTelemetryInitializer(sdkVersionProvider, roleInstanceProvider, provider.GetService<IOptions<ApplicationInsightsLoggerOptions>>()));
                }

                SetupTelemetryConfiguration(
                    config,
                    options,
                    channel,
                    provider.GetServices<ITelemetryInitializer>(),
                    provider.GetServices<ITelemetryModule>(),
                    appIdProvider,
                    filterOptions,
                    roleInstanceProvider,
                    provider.GetService<QuickPulseInitializationScheduler>(),
                    additionalTelemetryConfig);

                return config;
            });

            services.AddSingleton<TelemetryClient>(provider =>
            {
                TelemetryConfiguration configuration = provider.GetService<TelemetryConfiguration>();
                TelemetryClient client = new TelemetryClient(configuration);

                ISdkVersionProvider versionProvider = provider.GetService<ISdkVersionProvider>();
                client.Context.GetInternalContext().SdkVersion = versionProvider?.GetSdkVersion();

                return client;
            });

            services.AddSingleton<ILoggerProvider, ApplicationInsightsLoggerProvider>();

            if (loggerOptionsConfiguration != null)
            {
                services.Configure<ApplicationInsightsLoggerOptions>(loggerOptionsConfiguration);
            }

            return services;
        }

        internal static LoggerFilterOptions CreateFilterOptions(LoggerFilterOptions registeredOptions)
        {
            // We want our own copy of the rules, excluding the 'allow-all' rule that we added for this provider.
            LoggerFilterOptions customFilterOptions = new LoggerFilterOptions
            {
                MinLevel = registeredOptions.MinLevel
            };

            ApplicationInsightsLoggerFilterRule allowAllRule = registeredOptions.Rules.OfType<ApplicationInsightsLoggerFilterRule>().Single();

            // Copy all existing rules
            foreach (LoggerFilterRule rule in registeredOptions.Rules)
            {
                if (rule != allowAllRule)
                {
                    customFilterOptions.Rules.Add(rule);
                }
            }

            // Copy 'hidden' rules
            foreach (LoggerFilterRule rule in allowAllRule.ChildRules)
            {
                customFilterOptions.Rules.Add(rule);
            }

            return customFilterOptions;
        }

        private static void SetupTelemetryConfiguration(
            TelemetryConfiguration configuration,
            ApplicationInsightsLoggerOptions options,
            ITelemetryChannel channel,
            IEnumerable<ITelemetryInitializer> telemetryInitializers,
            IEnumerable<ITelemetryModule> telemetryModules,
            IApplicationIdProvider applicationIdProvider,
            LoggerFilterOptions filterOptions,
            IRoleInstanceProvider roleInstanceProvider,
            QuickPulseInitializationScheduler delayer,
            Action<TelemetryConfiguration> additionalTelemetryConfig)
        {
            if (options.ConnectionString != null)
            {
                configuration.ConnectionString = options.ConnectionString;
            }
            else if (options.InstrumentationKey != null)
            {
                configuration.InstrumentationKey = options.InstrumentationKey;
            }

            // Default is connection string based ingestion
            if (options.TokenCredentialOptions?.CreateTokenCredential() is TokenCredential credential)
            {
                configuration.SetAzureTokenCredential(credential);
            }

            configuration.TelemetryChannel = channel;

            foreach (ITelemetryInitializer initializer in telemetryInitializers)
            {
                if (!(initializer is NullTelemetryInitializer))
                {
                    configuration.TelemetryInitializers.Add(initializer);
                }
            }

            (channel as ServerTelemetryChannel)?.Initialize(configuration);

            QuickPulseTelemetryModule quickPulseModule = null;
            foreach (ITelemetryModule module in telemetryModules)
            {
                if (module is QuickPulseTelemetryModule telemetryModule)
                {
                    quickPulseModule = telemetryModule;
                    if (options.LiveMetricsAuthenticationApiKey != null)
                    {
                        quickPulseModule.AuthenticationApiKey = options.LiveMetricsAuthenticationApiKey;
                    }

                    quickPulseModule.ServerId = roleInstanceProvider?.GetRoleInstanceName();

                    // QuickPulse can have a startup performance hit, so delay its initialization.
                    delayer.ScheduleInitialization(() => module.Initialize(configuration), options.LiveMetricsInitializationDelay);
                }
                else if (module != null)
                {
                    module.Initialize(configuration);
                }
            }

            // Metrics extractor must be added before filtering and adaptive sampling telemetry processor to account for all the data.
            if (options.EnableAutocollectedMetricsExtractor)
            {
                configuration.TelemetryProcessorChainBuilder
                    .Use((next) => new AutocollectedMetricsExtractor(next));
            }

            QuickPulseTelemetryProcessor quickPulseProcessor = null;
            configuration.TelemetryProcessorChainBuilder
                .Use((next) => new OperationFilteringTelemetryProcessor(next));

            if (options.EnableLiveMetrics)
            {
                configuration.TelemetryProcessorChainBuilder.Use((next) =>
                {
                    quickPulseProcessor = new QuickPulseTelemetryProcessor(next);
                    return quickPulseProcessor;
                });
            }

            // No need to "late filter" as the logs will already be filtered before they are sent to the Logger.
            if (filterOptions != null)
            {
                configuration.TelemetryProcessorChainBuilder.Use((next) => new FilteringTelemetryProcessor(filterOptions, next));
            }

            if (options.SamplingSettings != null)
            {
                configuration.TelemetryProcessorChainBuilder.Use((next) =>
                {
                    if (options.EnableAdaptiveSamplingDelay)
                    {
                        return new DelayedSamplingProcessor(next, options);
                    }
                    else
                    {
                        return TelemetryProcessorFactory.CreateAdaptiveSamplingProcessor(options, next);
                    }
                });
            }

            additionalTelemetryConfig?.Invoke(configuration);

            if (options.SnapshotConfiguration != null)
            {
                configuration.TelemetryProcessorChainBuilder.UseSnapshotCollector(options.SnapshotConfiguration);
            }

            configuration.TelemetryProcessorChainBuilder.Build();
            quickPulseModule?.RegisterTelemetryProcessor(quickPulseProcessor);

            foreach (ITelemetryProcessor processor in configuration.TelemetryProcessors)
            {
                if (processor is ITelemetryModule module)
                {
                    module.Initialize(configuration);
                }
            }

            configuration.ApplicationIdProvider = applicationIdProvider;

            _source.Write(EventName, "ApplicationInsightsServiceCollectionExtensions, telemetry configuration-" + configuration.ToString() + configuration.LogConfiguration());
        }
    }

    public static class EnhancedTelemetryConfigurationInspector
    {
        public static string InspectConfiguration(TelemetryConfiguration config)
        {
            var result = new StringBuilder();
            result.AppendLine("=== Enhanced TelemetryConfiguration Inspection ===");

            if (config == null)
            {
                result.AppendLine("Configuration is null");
                return result.ToString();
            }

            // Inspect all public properties of TelemetryConfiguration
            var properties = typeof(TelemetryConfiguration).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(config);
                    result.AppendLine($"{prop.Name}: {FormatValue(value)}");

                    // Dedicated inspection for key collections
                    if (prop.Name == "TelemetryInitializers" && value != null)
                    {
                        InspectInitializers(result, value);
                    }
                    else if (prop.Name == "TelemetryProcessors" && value != null)
                    {
                        InspectProcessors(result, value);
                    }
                    else if (prop.Name == "TelemetrySinks" && value != null)
                    {
                        InspectSinks(result, value);
                    }
                    else if (prop.Name == "TelemetryModules" && value != null)
                    {
                        InspectTelemetryModules(result, value);
                    }
                    else if (prop.Name == "TelemetryChannel" && value != null)
                    {
                        InspectTelemetryChannelDetails(result, value);
                    }
                }
                catch (Exception ex)
                {
                    result.AppendLine($"{prop.Name}: [Error: {ex.Message}]");
                }
            }

            return result.ToString();
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is string str)
            {
                return $"\"{str}\"";
            }

            if (value is bool b)
            {
                return b.ToString().ToLower();
            }

            if (value.GetType().IsValueType)
            {
                return value.ToString();
            }

            if (value is ICollection collection)
            {
                return $"Collection (Count: {collection.Count})";
            }

            return $"{value.GetType().Name}: {value}";
        }

        private static void InspectInitializers(StringBuilder result, object initializers)
        {
            result.AppendLine("  Telemetry Initializers:");
            var collection = initializers as IEnumerable;
            if (collection != null)
            {
                int i = 0;
                foreach (var item in collection)
                {
                    result.AppendLine($"    [{i}] {item.GetType().Name}");
                    i++;
                }
            }
        }

        private static void InspectProcessors(StringBuilder result, object processors)
        {
            result.AppendLine("  Telemetry Processors:");
            var collection = processors as IEnumerable;
            if (collection != null)
            {
                int i = 0;
                foreach (var item in collection)
                {
                    result.AppendLine($"    [{i}] {item.GetType().Name}");
                    // Optionally, inspect sampling processors here
                    i++;
                }
            }
        }

        private static void InspectSinks(StringBuilder result, object sinks)
        {
            result.AppendLine("  Telemetry Sinks:");
            var collection = sinks as IEnumerable;
            if (collection != null)
            {
                int i = 0;
                foreach (var item in collection)
                {
                    result.AppendLine($"    [{i}] {item.GetType().Name}");
                    i++;
                }
            }
        }

        // CRITICAL: Inspect TelemetryModules for RequestTrackingTelemetryModule
        private static void InspectTelemetryModules(StringBuilder result, object modules)
        {
            result.AppendLine("  === Telemetry Modules (CRITICAL for Request Tracking) ===");
            var collection = modules as IEnumerable;
            if (collection != null)
            {
                int index = 0;
                bool foundRequestModule = false;
                foreach (var item in collection)
                {
                    var moduleType = item.GetType().Name;
                    result.AppendLine($"    [{index}] {moduleType}");
                    if (moduleType.Contains("RequestTracking"))
                    {
                        foundRequestModule = true;
                        InspectRequestTrackingModule(result, item);
                    }
                    index++;
                }

                if (!foundRequestModule)
                {
                    result.AppendLine("    *** WARNING: No RequestTrackingTelemetryModule found! ***");
                    result.AppendLine("    *** This explains missing request telemetry! ***");
                }
            }
            else
            {
                result.AppendLine("    [Collection is null or not enumerable]");
            }
        }

        // Inspect details of RequestTrackingTelemetryModule
        private static void InspectRequestTrackingModule(StringBuilder result, object module)
        {
            result.AppendLine("      === RequestTrackingTelemetryModule Details ===");
            try
            {
                var type = module.GetType();
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(module);
                        if (prop.Name == "Handlers" && value != null)
                        {
                            result.AppendLine($"        {prop.Name}:");
                            InspectHandlers(result, value);
                        }
                        else
                        {
                            result.AppendLine($"        {prop.Name}: {FormatValue(value)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"        {prop.Name}: [Error: {ex.Message}]");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"      [Error inspecting RequestTrackingModule: {ex.Message}]");
            }
        }

        private static void InspectHandlers(StringBuilder result, object handlers)
        {
            var collection = handlers as IEnumerable;
            if (collection != null)
            {
                int idx = 0;
                foreach (var handler in collection)
                {
                    result.AppendLine($"          [{idx}] {handler}");
                    idx++;
                }
                if (idx == 0)
                {
                    result.AppendLine("          [No handlers configured - all requests will be tracked]");
                }
            }
        }

        // Inspect TelemetryChannel deeply (especially if request telemetry is missing)
        private static void InspectTelemetryChannelDetails(StringBuilder result, object channel)
        {
            result.AppendLine("  === Telemetry Channel Details ===");
            try
            {
                var channelType = channel.GetType();
                var properties = channelType.GetProperties();
                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(channel);
                        result.AppendLine($"    {prop.Name}: {FormatValue(value)}");
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"    {prop.Name}: [Error: {ex.Message}]");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"  [Error inspecting TelemetryChannel: {ex.Message}]");
            }
        }

        // Extension method for usage convenience
        public static string LogConfiguration(this TelemetryConfiguration config)
        {
            return InspectConfiguration(config);
        }
    }
}