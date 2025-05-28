### Release notes

<!-- Please add your release notes in the following format:
- My change description (#PR)
-->
- Improved memory metrics reporting using CGroup data for Linux consumption (#10968)
- Memory allocation optimizations in `RpcWorkerConfigFactory.AddProviders` (#10959)
- Fixing GrpcWorkerChannel concurrency bug (#10998)
- Avoid circular dependency when resolving LinuxContainerLegionMetricsPublisher. (#10991)
- Add 'unix' to the list of runtimes kept when importing PowerShell worker for Linux builds
- Update PowerShell 7.4 worker to 4.0.4206
- Memory allocation optimizations in `ScriptStartupTypeLocator.GetExtensionsStartupTypesAsync` (#11012)
- Fix invocation timeout when incoming request contains "x-ms-invocation-id" header (#10980)
- Warn if .azurefunctions folder does not exist (#10967)
- Memory allocation & CPU optimizations in `GrpcMessageExtensionUtilities.ConvertFromHttpMessageToExpando` (#11054)
- Replace `Timer` with `while`-loop in `FlexConsumptionMetricsPublisher` (#11071)
- Memory allocation optimizations in `ReadLanguageWorkerFile` by reading files in buffered chunks, preventing LOH allocations (#11069)
- Enhancing the capability to send startup failure logs to AppInsights/Otel. (#11055)
- Added support for collecting cross-platform perf traces and generating PGO JIT traces (#11062)
- Memory allocation optimizations in `DependencyHelper.GetExtensionRequirements` (#11022)
- Fix Instance Manager for CV1 Migration (#11072)
- Avoid setting up OTel and AzMon exporter in the placeholder mode. (#11090)
- Update Java Worker Version to [2.19.1](https://github.com/Azure/azure-functions-java-worker/releases/tag/2.19.1)
