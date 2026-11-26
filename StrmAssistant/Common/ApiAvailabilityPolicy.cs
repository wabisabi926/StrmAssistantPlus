using System;
using StrmAssistant.Core;

namespace StrmAssistant.Common
{
    internal static class ApiAvailabilityPolicy
    {
        public static bool MissingComponentsAreUnsupported(EmbyApiVersion version)
        {
            return version >= EmbyApiVersion.V4_11;
        }
    }
}
