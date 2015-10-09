// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Text;
using global::System.Runtime;
using global::System.Diagnostics;
using global::System.Diagnostics.Contracts;

using global::Internal.Runtime.Augments;

namespace Internal.DeveloperExperience
{
    public class DeveloperExperience
    {
        public virtual void WriteLine(String s)
        {
            Debug.WriteLine(s);
            return;
        }

        public virtual String CreateStackTraceString(IntPtr ip, bool includeFileInfo)
        {
            ReflectionExecutionDomainCallbacks reflectionCallbacks = RuntimeAugments.CallbacksIfAvailable;
            if (reflectionCallbacks != null)
            {
                IntPtr methodStart = RuntimeImports.RhFindMethodStartAddress(ip);
                if (methodStart != IntPtr.Zero)
                {
                    string methodName = string.Empty;
                    try
                    {
                        methodName = reflectionCallbacks.GetMethodNameFromStartAddressIfAvailable(methodStart);
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(methodName))
                        return methodName;
                }
            }

            String fullPathToApplication = RuntimeAugments.TryGetFullPathToMainApplication();
            if (string.IsNullOrEmpty(fullPathToApplication))
                return "<unknown>";

            StringBuilder sb = new StringBuilder();
            String fileNameWithoutExtension = GetFileNameWithoutExtension(fullPathToApplication);
            int rva = RuntimeAugments.ConvertIpToRva(ip);
            sb.Append(fileNameWithoutExtension);
            sb.Append("!<BaseAddress>+0x");
            sb.Append(rva.ToString("x"));
            return sb.ToString();
        }

        public virtual void TryGetSourceLineInfo(IntPtr ip, out string fileName, out int lineNumber, out int columnNumber)
        {
            fileName = null;
            lineNumber = 0;
            columnNumber = 0;
        }

        public virtual bool OnContractFailure(String stackTrace, ContractFailureKind contractFailureKind, String displayMessage, String userMessage, String conditionText, Exception innerException)
        {
            Debug.WriteLine("Assertion failed: " + (displayMessage == null ? "" : displayMessage));
            if (Debugger.IsAttached)
                Debugger.Break();
            return false;
        }

        public static DeveloperExperience Default
        {
            get
            {
                DeveloperExperience result = s_developerExperience;
                if (result == null)
                    return new DeveloperExperience(); // Provide the bare-bones default if a custom one hasn't been supplied.
                return result;
            }

            set
            {
                s_developerExperience = value;
            }
        }

        private static String GetFileNameWithoutExtension(String path)
        {
            path = GetFileName(path);
            int i;
            if ((i = path.LastIndexOf('.')) == -1)
                return path; // No path extension found
            else
                return path.Substring(0, i);
        }

        private static String GetFileName(String path)
        {
            int length = path.Length;
            for (int i = length; --i >= 0;)
            {
                char ch = path[i];
                if (ch == '/' || ch == '\\' || ch == ':')
                    return path.Substring(i + 1, length - i - 1);
            }
            return path;
        }

        private static DeveloperExperience s_developerExperience;
    }
}

