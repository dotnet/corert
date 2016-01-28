// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.IO;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Threading;

using global::System.Reflection.Runtime.General;

using global::Internal.Reflection.Core;
using global::Internal.Runtime.Augments;
using global::Internal.Reflection.Core.Execution;

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution
{
    //=============================================================================================================================
    // The assembly resolution policy for Project N's emulation of "classic reflection."
    //
    // The policy is very simple: the only assemblies that can be "loaded" are those that are statically linked into the running
    // native process. There is no support for probing for assemblies in directories, user-supplied files, GACs, NICs or any
    // other repository.
    //=============================================================================================================================
    internal sealed class AssemblyBinderImplementation : AssemblyBinder
    {
        public AssemblyBinderImplementation(ExecutionEnvironmentImplementation executionEnvironment)
        {
            _executionEnvironment = executionEnvironment;
            _readers = new LowLevelListWithIList<MetadataReader>();
            _scopeGroups = new LowLevelDictionaryWithIEnumerable<string, ScopeDefinitionGroup>();
        }

        /// <summary>
        /// Install callback that gets called whenever a module gets registered. Unfortunately we cannot do that
        /// in the constructor as the callback gets called immediately for the modules that have
        /// already been registered and at the time AssemblyBinderImplementation is constructed
        /// the reflection execution engine is not yet fully initialized - in particular, AddScopesFromReaderToGroups
        /// calls AssemblyName.FullName which requires ReflectionAugments.ReflectionCoreCallbacks which
        /// are registered in ReflectionCoreExecution after the execution domain gets initialized
        /// and the execution domain initialization requires ReflectionDomainSetup which constructs
        /// the AssemblyBinderImplementation. Sigh.
        /// </summary>
        public void InstallModuleRegistrationCallback()
        {
            _executionEnvironment.AddModuleRegistrationCallback(RegisterModule);
        }

        public sealed override bool Bind(AssemblyName refName, out MetadataReader reader, out ScopeDefinitionHandle scopeDefinitionHandle, out IEnumerable<QScopeDefinition> overflowScopes, out Exception exception)
        {
            bool foundMatch = false;
            reader = null;
            scopeDefinitionHandle = default(ScopeDefinitionHandle);
            exception = null;
            overflowScopes = null;

            foreach (KeyValuePair<string, ScopeDefinitionGroup> group in ScopeGroups)
            {
                AssemblyName defName = new AssemblyName(group.Key);
                if (AssemblyNameMatches(refName, defName))
                {
                    if (foundMatch)
                    {
                        exception = new AmbiguousMatchException();
                        return false;
                    }

                    foundMatch = true;
                    ScopeDefinitionGroup scopeDefinitionGroup = group.Value;

                    reader = scopeDefinitionGroup.CanonicalScope.Reader;
                    scopeDefinitionHandle = scopeDefinitionGroup.CanonicalScope.Handle;
                    overflowScopes = scopeDefinitionGroup.OverflowScopes;
                }
            }

            if (!foundMatch)
            {
                exception = new IOException(SR.Format(SR.FileNotFound_AssemblyNotFound, refName.FullName));
                return false;
            }

            return true;
        }

        //
        // Encapsulates the assembly ref->def matching policy.
        //
        private bool AssemblyNameMatches(AssemblyName refName, AssemblyName defName)
        {
            //
            // The defName came from trusted metadata so it should be fully specified.
            //
            Debug.Assert(defName.Version != null);
            Debug.Assert(defName.CultureName != null);

            // At least one real-world app calls Type.GetType() for "char" using the assembly name "mscorlib". To accomodate this,
            // we will adopt the desktop CLR rule that anything named "mscorlib" automatically binds to the core assembly.
            if (refName.Name == "mscorlib")
            {
                AssemblyName coreAssemblyName = new AssemblyName(ReflectionExecution.DefaultAssemblyNameForGetType);
                if (defName.Name != coreAssemblyName.Name)
                    return false;
                byte[] defPkt = defName.GetPublicKeyToken();
                if (defPkt == null)
                    return false;
                if (!ArePktsEqual(defPkt, coreAssemblyName.GetPublicKeyToken()))
                    return false;
                return true;
            }

            if (!(refName.Name.Equals(defName.Name, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (refName.Version != null)
            {
                int compareResult = refName.Version.CompareTo(defName.Version);
                if (compareResult > 0)
                    return false;
            }

            if (refName.CultureName != null)
            {
                if (!(refName.CultureName.Equals(defName.CultureName)))
                    return false;
            }

            // Bartok cannot handle const enums for now.
            /*const*/
            AssemblyNameFlags ignorableFlags = AssemblyNameFlags.PublicKey;
            if ((refName.Flags & ~ignorableFlags) != (defName.Flags & ~ignorableFlags))
            {
                return false;
            }

            byte[] refPublicKeyToken = refName.GetPublicKeyToken();
            if (refPublicKeyToken != null)
            {
                byte[] defPublicKeyToken = defName.GetPublicKeyToken();
                if (defPublicKeyToken == null)
                    return false;
                if (!ArePktsEqual(refPublicKeyToken, defPublicKeyToken))
                    return false;
            }

            return true;
        }


        internal new AssemblyName CreateAssemblyNameFromMetadata(MetadataReader reader, ScopeDefinitionHandle scopeDefinitionHandle)
        {
            return base.CreateAssemblyNameFromMetadata(reader, scopeDefinitionHandle);
        }

        /// <summary>
        /// This callback gets called whenever a module gets registered. It adds the metadata reader
        /// for the new module to the available scopes. The lock in ExecutionEnvironmentImplementation ensures
        /// that this function may never be called concurrently so that we can assume that two threads
        /// never update the reader and scope list at the same time.
        /// </summary>
        /// <param name="moduleHandle">Module handle to register</param>
        private void RegisterModule(IntPtr moduleHandle)
        {
            MetadataReader newReader = _executionEnvironment.GetMetadataReaderForModule(moduleHandle);
            if (newReader == null)
            {
                return;
            }

            // Build new reader list
            LowLevelListWithIList<MetadataReader> readers = new LowLevelListWithIList<MetadataReader>(_readers.Count + 1);
            foreach (MetadataReader oldReader in _readers)
            {
                readers.Add(oldReader);
            }
            readers.Add(newReader);

            LowLevelDictionaryWithIEnumerable<string, ScopeDefinitionGroup> scopeGroups = new LowLevelDictionaryWithIEnumerable<string, ScopeDefinitionGroup>();
            foreach (KeyValuePair<string, ScopeDefinitionGroup> oldGroup in _scopeGroups)
            {
                scopeGroups.Add(oldGroup.Key, oldGroup.Value);
            }
            AddScopesFromReaderToGroups(scopeGroups, newReader);

            // Update reader and scope list
            _readers = readers;
            _scopeGroups = scopeGroups;
        }

        private LowLevelListWithIList<MetadataReader> Readers
        {
            get
            {
                return _readers;
            }
        }

        private LowLevelDictionaryWithIEnumerable<string, ScopeDefinitionGroup> ScopeGroups
        {
            get
            {
                return _scopeGroups;
            }
        }

        private void AddScopesFromReaderToGroups(LowLevelDictionaryWithIEnumerable<string, ScopeDefinitionGroup> groups, MetadataReader reader)
        {
            foreach (ScopeDefinitionHandle scopeDefinitionHandle in reader.ScopeDefinitions)
            {
                AssemblyName defName = this.CreateAssemblyNameFromMetadata(reader, scopeDefinitionHandle);
                string defFullName = defName.FullName;

                ScopeDefinitionGroup scopeDefinitionGroup;
                if (groups.TryGetValue(defFullName, out scopeDefinitionGroup))
                {
                    scopeDefinitionGroup.AddOverflowScope(new QScopeDefinition(reader, scopeDefinitionHandle));
                }
                else
                {
                    scopeDefinitionGroup = new ScopeDefinitionGroup(new QScopeDefinition(reader, scopeDefinitionHandle));
                    groups.Add(defFullName, scopeDefinitionGroup);
                }
            }
        }

        private static bool ArePktsEqual(byte[] pkt1, byte[] pkt2)
        {
            if (pkt1.Length != pkt2.Length)
                return false;
            for (int i = 0; i < pkt1.Length; i++)
            {
                if (pkt1[i] != pkt2[i])
                    return false;
            }
            return true;
        }

        private volatile LowLevelListWithIList<MetadataReader> _readers;
        private volatile LowLevelDictionaryWithIEnumerable<string, ScopeDefinitionGroup> _scopeGroups;

        private ExecutionEnvironmentImplementation _executionEnvironment;

        private class ScopeDefinitionGroup
        {
            public ScopeDefinitionGroup(QScopeDefinition canonicalScope)
            {
                _canonicalScope = canonicalScope;
            }

            public QScopeDefinition CanonicalScope { get { return _canonicalScope; } }

            public IEnumerable<QScopeDefinition> OverflowScopes
            {
                get
                {
                    if (_overflowScopes == null)
                    {
                        return Empty<QScopeDefinition>.Enumerable;
                    }

                    return _overflowScopes.ToArray();
                }
            }

            public void AddOverflowScope(QScopeDefinition overflowScope)
            {
                if (_overflowScopes == null)
                {
                    _overflowScopes = new LowLevelListWithIList<QScopeDefinition>();
                }

                _overflowScopes.Add(overflowScope);
            }

            private readonly QScopeDefinition _canonicalScope;
            private LowLevelListWithIList<QScopeDefinition> _overflowScopes;
        }
    }
}


