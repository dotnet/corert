// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Internal.TypeSystem;

namespace ILCompiler
{
    public class UtcStackTraceEmissionPolicy : StackTraceEmissionPolicy
    {
        /// <summary>
        /// List of exception files to load.
        /// </summary>
        List<string> _stackTraceExceptionFiles = new List<string>();

        /// <summary>
        /// Explicitly blacklisted namespaces.
        /// </summary>
        HashSet<string> _namespaceBlacklist = new HashSet<string>();

        /// <summary>
        /// Explicitly whitelisted namespaces.
        /// </summary>
        HashSet<string> _namespaceWhitelist = new HashSet<string>();

        /// <summary>
        /// Explicitly blacklisted types.
        /// </summary>
        HashSet<string> _typeBlacklist = new HashSet<string>();

        /// <summary>
        /// Explicitly whitelisted types.
        /// </summary>
        HashSet<string> _typeWhitelist = new HashSet<string>();

        /// <summary>
        /// Cache of explicitly enabled / disabled types after their eligibility has been resolved.
        /// </summary>
        Dictionary<TypeDesc, bool> _cachedTypeEligibility = new Dictionary<TypeDesc, bool>();

        public UtcStackTraceEmissionPolicy()
        {
            // load the default exception file next to the NUTC app
            LoadExceptionFile(Path.Combine(GetAppExeDirectory(), "StackTraceExceptions.txt"));
        }

        public override bool ShouldIncludeMethod(MethodDesc method)
        {
            DefType type = method.GetTypicalMethodDefinition().OwningType as DefType;
            if (type != null)
                return !IsTypeExplicitlyDisabled(type);
            return false;
        }
        
        /// <summary>
        /// Check explicit type / namespace blacklist and update the cache.
        /// </summary>
        bool IsTypeExplicitlyDisabled(DefType type)
        {
            bool result;
            if (_cachedTypeEligibility.TryGetValue(type, out result))
            {
                return result;
            }

            string typeName;
            result = IsTypeExplicitlyDisabledInner(type, out typeName);
            _cachedTypeEligibility.Add(type, result);
            return result;
        }

        /// <summary>
        /// Check explicit type / namespace blacklist ignoring cache management. Optionally output the type name.
        /// </summary>
        bool IsTypeExplicitlyDisabledInner(DefType type, out string outputTypeName)
        {
            string typeName;
            bool isTypeDisabled = false;
            MetadataType metaDataType = type as MetadataType;

            if (metaDataType != null && metaDataType.ContainingType != null)
            {
                // Nested type
                isTypeDisabled = IsTypeExplicitlyDisabledInner(metaDataType.ContainingType, out typeName);
                typeName = typeName + '+' + metaDataType.Name;
            }
            else
            {
                // Namespace type
                typeName = type.Name;
                int lastPeriod = typeName.LastIndexOf('.');
                string namespaceName = null;

                if (lastPeriod != -1)
                {
                    namespaceName = typeName.Substring(0, lastPeriod);
                }

                isTypeDisabled = _namespaceBlacklist.Contains(namespaceName) && _namespaceWhitelist.Contains(namespaceName);
            }

            if (_typeBlacklist.Contains(typeName))
            {
                isTypeDisabled = true;
            }

            if (_typeWhitelist.Contains(typeName))
            {
                isTypeDisabled = false;
            }

            outputTypeName = typeName;
            return isTypeDisabled;
        }
        
        /// <summary>
        /// Identify the directory where the main executable resides.
        /// </summary>
        string GetAppExeDirectory()
        {
#if PROJECTN
            var process = Process.GetCurrentProcess();
            string fullPath = process.MainModule.FileName;
            return Path.GetDirectoryName(fullPath);
#else
            Debug.Assert(false);
            return null;
#endif
        }

        /// <summary>
        /// Load the default exception file and possibly additional custom stack trace exception files.
        /// </summary>
        void LoadExceptionFile(string exceptionFileName)
        {
#if PROJECTN
            if (!File.Exists(exceptionFileName))
                return;

            const int LeaderCharacterCount = 2;

            using (TextReader tr = File.OpenText(exceptionFileName))
            {
                string line = tr.ReadLine();
                while (line != null)
                {
                    // Currently supported leader character sequences are:
                    // N+nnn .. explicitly whitelist namespace nnn
                    // N-nnn .. explicitly blacklist namespace nnn
                    // T+ttt .. explicitly whitelist namespace-qualified type ttt (nested types separated by +)
                    // T-ttt .. explicitly blacklist namespace-qualified type ttt
                    // Type specifications override namespace specifications.
                    // Whenever both whitelist and blacklist is specified for an element, whitelist wins.

                    // Reserve empty lines and lines starting with # for comments
                    if (line.Length > LeaderCharacterCount && line[0] != '#')
                    {
                        char char1 = line[0];
                        char char2 = line[1];
                        string arg = line.Substring(2);

                        if (char1 == 'N' && char2 == '+')
                        {
                            _namespaceWhitelist.Add(arg);
                        }
                        else if (char1 == 'N' && char2 == '-')
                        {
                            _namespaceBlacklist.Add(arg);
                        }
                        else if (char1 == 'T' && char2 == '+')
                        {
                            _typeWhitelist.Add(arg);
                        }
                        else if (char1 == 'T' && char2 == '-')
                        {
                            _typeBlacklist.Add(arg);
                        }
                        else
                        {
                            // unexpected pattern
                            Debug.Assert(false);
                        }
                    }

                    line = tr.ReadLine();
                }
            }
#endif
        }
    }
}
