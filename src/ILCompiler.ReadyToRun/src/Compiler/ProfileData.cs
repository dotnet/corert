// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysis.ReadyToRun;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.Win32Resources;
using ILCompiler.IBC;
using System.Text;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    [Flags]
    public enum MethodProfilingDataFlags
    {
        // Important: update toolbox\ibcmerge\ibcmerge.cs if you change these
        ReadMethodCode = 0,  // 0x00001  // Also means the method was executed
        ReadMethodDesc = 1,  // 0x00002
        RunOnceMethod = 2,  // 0x00004
        RunNeverMethod = 3,  // 0x00008
                             //  MethodStoredDataAccess        = 4,  // 0x00010  // obsolete
        WriteMethodDesc = 5,  // 0x00020
                              //  ReadFCallHash                 = 6,  // 0x00040  // obsolete
        ReadGCInfo = 7,  // 0x00080
        CommonReadGCInfo = 8,  // 0x00100
                               //  ReadMethodDefRidMap           = 9,  // 0x00200  // obsolete
        ReadCerMethodList = 10, // 0x00400
        ReadMethodPrecode = 11, // 0x00800
        WriteMethodPrecode = 12, // 0x01000
        ExcludeHotMethodCode = 13, // 0x02000  // Hot method should be excluded from the ReadyToRun image
        ExcludeColdMethodCode = 14, // 0x04000  // Cold method should be excluded from the ReadyToRun image
        DisableInlining = 15, // 0x08000  // Disable inlining of this method in optimized AOT native code
    }

    public class MethodProfileData
    {
        public MethodProfileData(MethodDesc method, MethodProfilingDataFlags flags, uint scenarioMask)
        {
            Method = method;
            Flags = flags;
            ScenarioMask = scenarioMask;
        }
        public readonly MethodDesc Method;
        public readonly MethodProfilingDataFlags Flags;
        public readonly uint ScenarioMask;
    }

    public abstract class ProfileData
    {
        public ProfileData()
        {
        }

        public abstract bool PartialNGen { get; }
        public abstract MethodProfileData GetMethodProfileData(MethodDesc m);
        public abstract IReadOnlyList<MethodProfileData> GetAllMethodProfileData();
        public abstract byte[] GetMethodBlockCount(MethodDesc m);
    }

    public class EmptyProfileData : ProfileData
    {
        public static EmptyProfileData Singleton = new EmptyProfileData();

        private EmptyProfileData()
        {
        }

        public override bool PartialNGen => false;

        public override MethodProfileData GetMethodProfileData(MethodDesc m)
        {
            return null;
        }

        public override IReadOnlyList<MethodProfileData> GetAllMethodProfileData()
        {
            return Array.Empty<MethodProfileData>();
        }

        public override byte[] GetMethodBlockCount(MethodDesc m)
        {
            return null;
        }
    }


    public class ProfileDataManager
    {
        private Logger Logger;
        private IBCProfileParser _ibcParser;

        public ProfileDataManager(Logger logger)
        {
            Logger = logger;
            _ibcParser = new IBCProfileParser(logger);
        }

        private Dictionary<ModuleDesc, ProfileData> _profileData = new Dictionary<ModuleDesc, ProfileData>();

        public ProfileData GetDataForModuleDesc(ModuleDesc moduleDesc)
        {
            lock(_profileData)
            {
                ProfileData computedProfileData;
                if (_profileData.TryGetValue(moduleDesc, out computedProfileData))
                    return computedProfileData;
                computedProfileData = ComputeDataForModuleDesc(moduleDesc);
                _profileData.Add(moduleDesc, computedProfileData);
                return computedProfileData;
            }
        }

        private ProfileData ComputeDataForModuleDesc(ModuleDesc moduleDesc)
        {
            EcmaModule ecmaModule = moduleDesc as EcmaModule;
            if (ecmaModule == null)
                return EmptyProfileData.Singleton;

            var profileData = _ibcParser.ParseIBCDataFromModule(ecmaModule);
            if (profileData == null)
                profileData = EmptyProfileData.Singleton;

            return profileData;
        }
    }
}
