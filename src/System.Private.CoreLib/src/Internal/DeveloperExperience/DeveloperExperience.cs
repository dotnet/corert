// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// #define DUMP_STACKTRACE_BLOB // uncomment this to print the entire stack trace blob's raw encoded bytes to the debug console.

using System;
using System.Collections.Generic;
using System.Runtime;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;

using Internal.Runtime;
using Internal.Runtime.Augments;
using Internal.DeveloperExperience.StackTrace;

namespace Internal.DeveloperExperience
{
    public class DeveloperExperience
    {
        // For every module, keep a map of StackTraceBlobIndexes for each module.
        private static LowLevelDictionary<IntPtr, StackTraceBlobIndex> s_stackTraceBlobIndexes;

        // Given an rva for a method, Walks through the binary, reading descriptors, to build up and return the method name.
        private unsafe static string CreateStackTraceString(StackTraceBlobIndex stackTraceBlobIndex, uint rva, byte* pBlob)
        {
            uint methodStartRva = rva;
            uint methodCount = *((uint*)(pBlob) + 1); // Skipping the magic byte
            uint* pRvaTable = ((uint*)(pBlob) + 5);   // Skipping the magic byte and the table sizes

            // Iterate through each rva.
            for (uint i = 0; i < methodCount; i++)
            {
                if (*pRvaTable != methodStartRva)
                {
                    pRvaTable++;
                }
                else
                {
                    // Create the method descriptor from the offset found.
                    StackTraceMethodDescriptor md = StackTraceMethodDescriptor.CreateFromBuffer(stackTraceBlobIndex, pBlob, stackTraceBlobIndex.methodOffsets[(int)i]);
                    return md.ToString();
                }
            }

            return null;
        }

        public virtual void WriteLine(String s)
        {
            Debug.WriteLine(s);
            return;
        }

        public virtual String CreateStackTraceString(IntPtr ip, bool includeFileInfo)
        {
            string methodName = String.Empty;
            IntPtr moduleBase = RuntimeImports.RhGetModuleFromPointer(ip);
            IntPtr methodStart = RuntimeImports.RhFindMethodStartAddress(ip);
            try
            {
                if (methodStart != IntPtr.Zero)
                {
                    int rva = RuntimeAugments.ConvertIpToRva(methodStart);
                    unsafe
                    {
                        byte* pBlob = null;
                        uint cbBlob = 0;
                        if (RuntimeImports.RhFindBlob(moduleBase, (uint)ReflectionMapBlob.StackTraceData, &pBlob, &cbBlob) && cbBlob > 0)
                        {
                            StackTraceBlobIndex stackTraceBlobIndex = null;
                            if (s_stackTraceBlobIndexes == null)
                            {
                                s_stackTraceBlobIndexes = new LowLevelDictionary<IntPtr, StackTraceBlobIndex>();
                            }
                            if (!s_stackTraceBlobIndexes.TryGetValue(moduleBase, out stackTraceBlobIndex))
                            {
#if DUMP_STACKTRACE_BLOB
                                Debug.WriteLine("Entire blob:");
                                for (int i = 0; i < cbBlob; i++)
                                {
                                    Debug.WriteLine(pBlob[i]);
                                }
#endif
                                stackTraceBlobIndex = StackTraceBlobIndex.BuildStackTraceBlobIndex(pBlob, cbBlob);
                                s_stackTraceBlobIndexes.Add(moduleBase, stackTraceBlobIndex);
                            }

                            methodName = CreateStackTraceString(stackTraceBlobIndex, (uint)rva, pBlob);
                            Debug.Assert(methodName != null, "If we have the blob, we better have all the method names for every methods in the module.");
                        }
                    }

                    if (methodName == null)
                    {
                        // If we can't find anything, default to reflection.
                        // This can happen if the app is not compiled with the /EnableTextualStackTrace flag
                        ReflectionExecutionDomainCallbacks reflectionCallbacks = RuntimeAugments.CallbacksIfAvailable;

                        if (reflectionCallbacks != null)
                        {
                            methodName = reflectionCallbacks.GetMethodNameFromStartAddressIfAvailable(methodStart);
                        }
                    }
                }
            }
            catch 
            {
                // Ignoring any error occurred while trying to figure out the methodName
            }

            if (methodName != null)
            {
                return methodName;
            }
            else
            {
                string moduleFullFileName = RuntimeAugments.TryGetFullPathToApplicationModule(moduleBase);

                // Without any callbacks or the ability to map ip correctly we better admit that we don't know
                if (string.IsNullOrEmpty(moduleFullFileName))
                {
                    return "<unknown>";
                }

                StringBuilder sb = new StringBuilder();
                string fileNameWithoutExtension = GetFileNameWithoutExtension(moduleFullFileName);
                sb.Append(fileNameWithoutExtension);
                sb.Append("!<BaseAddress>+0x");
                sb.Append(RuntimeAugments.ConvertIpToRva(ip).ToString("x"));
                return sb.ToString();
            }
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
            for (int i = length; --i >= 0; )
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
