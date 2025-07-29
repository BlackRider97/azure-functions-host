// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal static class LoggingConstants
    {
        public const string ZeroIpAddress = "0.0.0.0";
        public const string Unknown = "[Unknown]";
        public const string ClientIpKey = "ClientIp";
        public const string HostInstanceIdKey = "HostInstanceId";
    }

    internal static class DictionaryExtensions
    {
        public static T GetValueOrDefault<T>(this IDictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary != null && dictionary.TryGetValue(key, out value))
            {
                return (T)value;
            }

            return default;
        }
    }

    internal static class ReadOnlyDictionaryExtensions
    {
        public static T GetValueOrDefault<T>(this IReadOnlyDictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary != null && dictionary.TryGetValue(key, out value))
            {
                return (T)value;
            }

            return default;
        }
    }
}
