// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Debug = System.Diagnostics.Debug;

namespace Internal.JitInterface
{
    internal sealed class JitConfigProvider
    {
        private Dictionary<string, string> _config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private object _keepAlive; // Keeps callback delegates alive

        public IntPtr UnmanagedInstance
        {
            get;
        }

        /// <summary>
        /// Creates a new instance of <see cref="JitConfigProvider"/>.
        /// </summary>
        /// <param name="parameters">Name-value pairs separated by an equals sign.</param>
        public JitConfigProvider(IEnumerable<string> parameters)
        {
            foreach (var param in parameters)
            {
                int indexOfEquals = param.IndexOf('=');

                // We're skipping bad parameters without reporting.
                // This is not a mainstream feature that would need to be friendly.
                // Besides, to really validate this, we would also need to check that the config name is known.
                if (indexOfEquals < 1)
                    continue;

                string name = param.Substring(0, indexOfEquals);
                string value = param.Substring(indexOfEquals + 1);

                _config[name] = value;
            }

            UnmanagedInstance = CreateUnmanagedInstance();
        }

        public int GetIntConfigValue(string name, int defaultValue)
        {
            string stringValue;
            int intValue;
            if (_config.TryGetValue(name, out stringValue) &&
                Int32.TryParse(stringValue, out intValue))
            {
                return intValue;
            }

            return defaultValue;
        }

        public string GetStringConfigValue(string name)
        {
            string stringValue;
            if (_config.TryGetValue(name, out stringValue))
            {
                return stringValue;
            }

            return String.Empty;
        }

        #region Unmanaged instance

        private unsafe IntPtr CreateUnmanagedInstance()
        {
            // TODO: this potentially leaks memory, but since we only expect to have one per compilation,
            // it shouldn't matter...

            const int numCallbacks = 2;

            IntPtr* callbacks = (IntPtr*)Marshal.AllocCoTaskMem(sizeof(IntPtr) * numCallbacks);
            object[] delegates = new object[numCallbacks];

            var d0 = new __getIntConfigValue(getIntConfigValue);
            callbacks[0] = Marshal.GetFunctionPointerForDelegate(d0);
            delegates[0] = d0;

            var d1 = new __getStringConfigValue(getStringConfigValue);
            callbacks[1] = Marshal.GetFunctionPointerForDelegate(d1);
            delegates[1] = d1;

            _keepAlive = delegates;
            IntPtr instance = Marshal.AllocCoTaskMem(sizeof(IntPtr));
            *(IntPtr**)instance = callbacks;

            return instance;
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private unsafe delegate int __getIntConfigValue(IntPtr thisHandle, [MarshalAs(UnmanagedType.LPWStr)] string name, int defaultValue);
        private unsafe int getIntConfigValue(IntPtr thisHandle, string name, int defaultValue)
        {
            return GetIntConfigValue(name, defaultValue);
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private unsafe delegate int __getStringConfigValue(IntPtr thisHandle, [MarshalAs(UnmanagedType.LPWStr)] string name, char* retBuffer, int retBufferLength);
        private unsafe int getStringConfigValue(IntPtr thisHandle, string name, char* retBuffer, int retBufferLength)
        {
            string result = GetStringConfigValue(name);

            for (int i = 0; i < Math.Min(retBufferLength, result.Length); i++)
                retBuffer[i] = result[i];

            return result.Length;
        }

        #endregion
    }
}
