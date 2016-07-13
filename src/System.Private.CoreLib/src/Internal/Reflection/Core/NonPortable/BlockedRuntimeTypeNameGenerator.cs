// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class dispenses randomized strings (that serve as both the fake name and fake assembly container) for
    // reflection-blocked types.
    //
    // The names are randomized to prevent apps from hard-wiring dependencies on them or attempting to serialize them
    // across app execution.
    //
    internal static class BlockedRuntimeTypeNameGenerator
    {
        public static String GetNameForBlockedRuntimeType(RuntimeType type)
        {
            String name = s_blockedNameTable.GetOrAdd(type);
            return name;
        }

        private sealed class BlockedRuntimeTypeNameTable : ConcurrentUnifier<RuntimeType, String>
        {
            protected override String Factory(RuntimeType key)
            {
                uint count = s_counter++;
                return "$BlockedFromReflection_" + count.ToString() + "_" + Guid.NewGuid().ToString().Substring(0, 8);
            }

            private static uint s_counter;
        }

        private static BlockedRuntimeTypeNameTable s_blockedNameTable = new BlockedRuntimeTypeNameTable();
    }
}


