// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;
using TypeAttributes = System.Reflection.TypeAttributes;
using MethodAttributes = System.Reflection.MethodAttributes;
using FieldAttributes = System.Reflection.FieldAttributes;

namespace ILCompiler
{
    /// <summary>
    /// Represents a metadata policy that blocks implementations details.
    /// </summary>
    public sealed class BlockedInternalsBlockingPolicy : MetadataBlockingPolicy
    {
        private class ModuleBlockingState
        {
            public ModuleDesc Module { get; }
            public bool HasBlockedInternals { get; }
            public ModuleBlockingState(ModuleDesc module, bool hasBlockedInternals)
            {
                Module = module;
                HasBlockedInternals = hasBlockedInternals;
            }
        }

        private class BlockedModulesHashtable : LockFreeReaderHashtable<ModuleDesc, ModuleBlockingState>
        {
            protected override int GetKeyHashCode(ModuleDesc key) => key.GetHashCode();
            protected override int GetValueHashCode(ModuleBlockingState value) => value.Module.GetHashCode();
            protected override bool CompareKeyToValue(ModuleDesc key, ModuleBlockingState value) => Object.ReferenceEquals(key, value.Module);
            protected override bool CompareValueToValue(ModuleBlockingState value1, ModuleBlockingState value2) => Object.ReferenceEquals(value1.Module, value2.Module);
            protected override ModuleBlockingState CreateValueFromKey(ModuleDesc module)
            {
                bool moduleHasBlockingPolicy = module.GetType("System.Runtime.CompilerServices", "__BlockReflectionAttribute", false) != null;
                return new ModuleBlockingState(module, moduleHasBlockingPolicy);
            }
        }
        private BlockedModulesHashtable _blockedModules = new BlockedModulesHashtable();

        private class BlockingState
        {
            public EcmaType Type { get; }
            public bool IsBlocked { get; }
            public BlockingState(EcmaType type, bool isBlocked)
            {
                Type = type;
                IsBlocked = isBlocked;
            }
        }

        private class BlockedTypeHashtable : LockFreeReaderHashtable<EcmaType, BlockingState>
        {
            private readonly BlockedModulesHashtable _blockedModules;

            public BlockedTypeHashtable(BlockedModulesHashtable blockedModules)
            {
                _blockedModules = blockedModules;
            }

            protected override int GetKeyHashCode(EcmaType key) => key.GetHashCode();
            protected override int GetValueHashCode(BlockingState value) => value.Type.GetHashCode();
            protected override bool CompareKeyToValue(EcmaType key, BlockingState value) => Object.ReferenceEquals(key, value.Type);
            protected override bool CompareValueToValue(BlockingState value1, BlockingState value2) => Object.ReferenceEquals(value1.Type, value2.Type);
            protected override BlockingState CreateValueFromKey(EcmaType type)
            {
                bool isBlocked = false;
                if (_blockedModules.GetOrCreateValue(type.EcmaModule).HasBlockedInternals)
                {
                    isBlocked = ComputeIsBlocked(type);
                }

                return new BlockingState(type, isBlocked);
            }

            private bool ComputeIsBlocked(EcmaType type)
            {
                // <Module> type always gets metadata
                if (type.IsModuleType)
                    return false;

                // The various SR types used in Resource Manager always get metadata
                if (type.Name == "SR")
                    return false;

                var typeDefinition = type.MetadataReader.GetTypeDefinition(type.Handle);
                DefType containingType = type.ContainingType;
                if (containingType == null)
                {
                    if ((typeDefinition.Attributes & TypeAttributes.Public) == 0)
                    {
                        return true;
                    }
                }
                else
                {
                    if ((typeDefinition.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic)
                    {
                        return ComputeIsBlocked((EcmaType)containingType);
                    }
                    else
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        private BlockedTypeHashtable _blockedTypes;

        public BlockedInternalsBlockingPolicy()
        {
            _blockedTypes = new BlockedTypeHashtable(_blockedModules);
        }

        public override bool IsBlocked(MetadataType type)
        {
            Debug.Assert(type.IsTypeDefinition);

            var ecmaType = type as EcmaType;
            if (ecmaType == null)
                return true;

            return _blockedTypes.GetOrCreateValue(ecmaType).IsBlocked;
        }

        public override bool IsBlocked(MethodDesc method)
        {
            Debug.Assert(method.IsTypicalMethodDefinition);

            var ecmaMethod = method as EcmaMethod;
            if (ecmaMethod == null)
                return true;

            if (_blockedTypes.GetOrCreateValue((EcmaType)ecmaMethod.OwningType).IsBlocked)
                return true;

            if (_blockedModules.GetOrCreateValue(ecmaMethod.Module).HasBlockedInternals)
            {
                if ((ecmaMethod.Attributes & MethodAttributes.Public) != MethodAttributes.Public)
                    return true;
            }

            return false;
        }

        public override bool IsBlocked(FieldDesc field)
        {
            Debug.Assert(field.IsTypicalFieldDefinition);

            var ecmaField = field as EcmaField;
            if (ecmaField == null)
                return true;

            if (_blockedTypes.GetOrCreateValue((EcmaType)ecmaField.OwningType).IsBlocked)
                return true;

            if (_blockedModules.GetOrCreateValue(ecmaField.Module).HasBlockedInternals)
            {
                if ((ecmaField.Attributes & FieldAttributes.Public) != FieldAttributes.Public)
                    return true;
            }

            return false;
        }
    }
}
