using System;
using StrmAssistant.Core;

namespace StrmAssistant.Common
{
    internal static class DtoPatchPolicy
    {
        public static bool UseBatchDto(EmbyApiVersion version)
        {
            return version < EmbyApiVersion.V4_11;
        }
    }
}
