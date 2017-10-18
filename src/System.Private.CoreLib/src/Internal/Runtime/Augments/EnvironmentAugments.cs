// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static partial class EnvironmentAugments
    {
        public static string GetEnvironmentVariable(string variable)
        {
            if (variable == null)
                throw new ArgumentNullException(nameof(variable));
            return GetEnvironmentVariableCore(variable);
        }

        public static string GetEnvironmentVariable(string variable, EnvironmentVariableTarget target)
        {
            if (target == EnvironmentVariableTarget.Process)
                return GetEnvironmentVariable(variable);

            if (variable == null)
                throw new ArgumentNullException(nameof(variable));

            bool fromMachine = ValidateAndConvertRegistryTarget(target);
            return GetEnvironmentVariableFromRegistry(variable, fromMachine: fromMachine);
        }

        public static void SetEnvironmentVariable(string variable, string value)
        {
            ValidateVariableAndValue(variable, ref value);

            SetEnvironmentVariableCore(variable, value);
        }

        public static void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target)
        {
            if (target == EnvironmentVariableTarget.Process)
            {
                SetEnvironmentVariable(variable, value);
                return;
            }

            ValidateVariableAndValue(variable, ref value);

            bool fromMachine = ValidateAndConvertRegistryTarget(target);
            SetEnvironmentVariableFromRegistry(variable, value, fromMachine: fromMachine);
        }

        private static void ValidateVariableAndValue(string variable, ref string value)
        {
            const int MaxEnvVariableValueLength = 32767;

            if (variable == null)
                throw new ArgumentNullException(nameof(variable));

            if (variable.Length == 0)
                throw new ArgumentException(SR.Argument_StringZeroLength, nameof(variable));

            if (variable[0] == '\0')
                throw new ArgumentException(SR.Argument_StringFirstCharIsZero, nameof(variable));

            if (variable.Length >= MaxEnvVariableValueLength)
                throw new ArgumentException(SR.Argument_LongEnvVarValue, nameof(variable));

            if (variable.IndexOf('=') != -1)
                throw new ArgumentException(SR.Argument_IllegalEnvVarName, nameof(variable));

            if (string.IsNullOrEmpty(value) || value[0] == '\0')
            {
                // Explicitly null out value if it's empty
                value = null;
            }
            else if (value.Length >= MaxEnvVariableValueLength)
            {
                throw new ArgumentException(SR.Argument_LongEnvVarValue, nameof(value));
            }
        }

        public static IEnumerable<KeyValuePair<string, string>> EnumerateEnvironmentVariables(EnvironmentVariableTarget target)
        {
            if (target == EnvironmentVariableTarget.Process)
                return EnumerateEnvironmentVariables();

            bool fromMachine = ValidateAndConvertRegistryTarget(target);
            return EnumerateEnvironmentVariablesFromRegistry(fromMachine: fromMachine);
        }

        private static bool ValidateAndConvertRegistryTarget(EnvironmentVariableTarget target)
        {
            Debug.Assert(target != EnvironmentVariableTarget.Process);
            if (target == EnvironmentVariableTarget.Machine)
                return true;
            else if (target == EnvironmentVariableTarget.User)
                return false;
            else
                throw new ArgumentOutOfRangeException(nameof(target), target, SR.Format(SR.Arg_EnumIllegalVal, target));
        }

        public static int CurrentManagedThreadId => System.Threading.ManagedThreadId.Current;
        public static void FailFast(string message, Exception error) => RuntimeExceptionHelpers.FailFast(message, error);

        internal static void ShutdownCore()
        {
            // Here we'll handle AppDomain.ProcessExit, shut down threading etc.
        }

        private static int s_latchedExitCode;
        public static int ExitCode
        {
            get
            {
                return s_latchedExitCode;
            }
            set
            {
                s_latchedExitCode = value;
            }
        }

        public static bool HasShutdownStarted => false; // .NET Core does not have shutdown finalization

        public static string StackTrace
        {
            // Disable inlining to have predictable stack frame to skip
            [MethodImpl(MethodImplOptions.NoInlining)]
            get
            {
                // RhGetCurrentThreadStackTrace returns the number of frames(cFrames) added to input buffer.
                // It returns a negative value, -cFrames which is the required array size, if the buffer is too small.
                // Initial array length is deliberately chosen to be 0 so that we reallocate to exactly the right size
                // for StackFrameHelper.FormatStackTrace call. If we want to do this optimistically with one call change
                // FormatStackTrace to accept an explicit length.
                IntPtr[] frameIPs = Array.Empty<IntPtr>();
                int cFrames = RuntimeImports.RhGetCurrentThreadStackTrace(frameIPs);
                if (cFrames < 0)
                {
                    frameIPs = new IntPtr[-cFrames];
                    cFrames = RuntimeImports.RhGetCurrentThreadStackTrace(frameIPs);
                    if (cFrames < 0)
                    {
                        return "";
                    }
                }

                return Internal.Diagnostics.StackTraceHelper.FormatStackTrace(frameIPs, 1, true);
            }
        }

        public static int TickCount => Environment.TickCount;
    }
}
