// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Runtime;
using System.Diagnostics;
using System.Diagnostics.Contracts;

using Internal.Runtime.Augments;

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
            String moduleFullFileName = null;

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

                // If we don't have precise information, try to map it at least back to the right module.
                IntPtr moduleBase = RuntimeImports.RhGetModuleFromPointer(ip);
                moduleFullFileName = RuntimeAugments.TryGetFullPathToApplicationModule(moduleBase);
            }

            // Without any callbacks or the ability to map ip correctly we better admit that we don't know
            if (string.IsNullOrEmpty(moduleFullFileName))
            {
                return "<unknown>";
            }

            StringBuilder sb = new StringBuilder();
            String fileNameWithoutExtension = GetFileNameWithoutExtension(moduleFullFileName);
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

