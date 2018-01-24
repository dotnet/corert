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
        private enum ModuleBlockingMode
        {
            None,
            BlockedInternals,
            FullyBlocked,
        }

        private class ModuleBlockingState
        {
            public ModuleDesc Module { get; }
            public ModuleBlockingMode BlockingMode { get; }
            public ModuleBlockingState(ModuleDesc module, ModuleBlockingMode mode)
            {
                Module = module;
                BlockingMode = mode;
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
                ModuleBlockingMode blockingMode = ModuleBlockingMode.None;

                if (module.GetType("System.Runtime.CompilerServices", "__BlockAllReflectionAttribute", false) != null)
                {
                    blockingMode = ModuleBlockingMode.FullyBlocked;
                }
                else if (module.GetType("System.Runtime.CompilerServices", "__BlockReflectionAttribute", false) != null)
                {
                    blockingMode = ModuleBlockingMode.BlockedInternals;
                }

                return new ModuleBlockingState(module, blockingMode);
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
                ModuleBlockingMode moduleBlockingMode = _blockedModules.GetOrCreateValue(type.EcmaModule).BlockingMode;
                bool isBlocked = ComputeIsBlocked(type, moduleBlockingMode);
                return new BlockingState(type, isBlocked);
            }

            private bool ComputeIsBlocked(EcmaType type, ModuleBlockingMode blockingMode)
            {
                // If the type is explicitly blocked, it's always blocked.
                if (type.HasCustomAttribute("System.Runtime.CompilerServices", "ReflectionBlockedAttribute"))
                    return true;

                // If no blocking is applied to the module, the type is not blocked
                if (blockingMode == ModuleBlockingMode.None)
                    return false;

                // <Module> type always gets metadata
                if (type.IsModuleType)
                    return false;

                // The various SR types used in Resource Manager always get metadata
                if (type.Name == "SR")
                    return false;

                // We block everything else if the module is blocked
                if (blockingMode == ModuleBlockingMode.FullyBlocked)
                    return true;

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
                        return ComputeIsBlocked((EcmaType)containingType, blockingMode);
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

        private MetadataType _arrayOfTType;
        private MetadataType InitializeArrayOfTType(TypeSystemEntity contextEntity)
        {
            _arrayOfTType = contextEntity.Context.SystemModule.GetType("System", "Array`1");
            return _arrayOfTType;
        }
        private MetadataType GetArrayOfTType(TypeSystemEntity contextEntity)
        {
            if (_arrayOfTType != null)
            {
                return _arrayOfTType;
            }
            return InitializeArrayOfTType(contextEntity);
        }

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

            ModuleBlockingMode moduleBlockingMode = _blockedModules.GetOrCreateValue(ecmaMethod.Module).BlockingMode;
            if (moduleBlockingMode == ModuleBlockingMode.None)
                return false;
            else if (moduleBlockingMode == ModuleBlockingMode.FullyBlocked)
                return true;

            // We are blocking internal implementation details
            Debug.Assert(moduleBlockingMode == ModuleBlockingMode.BlockedInternals);

            if (_blockedTypes.GetOrCreateValue((EcmaType)ecmaMethod.OwningType).IsBlocked)
                return true;

            if ((ecmaMethod.Attributes & MethodAttributes.Public) != MethodAttributes.Public)
                return true;

            // Methods on Array`1<T> are implementation details that implement the generic interfaces on
            // arrays. They should not generate metadata or be reflection invokable.
            // We could get rid of this special casing two ways:
            // * Make these method stop being regular EcmaMethods with Array<T> as their owning type, or
            // * Make these methods implement the interfaces explicitly (they would become private and naturally blocked)
            if (ecmaMethod.OwningType == GetArrayOfTType(ecmaMethod))
                return true;

            return false;
        }

        public override bool IsBlocked(FieldDesc field)
        {
            Debug.Assert(field.IsTypicalFieldDefinition);

            var ecmaField = field as EcmaField;
            if (ecmaField == null)
                return true;

            ModuleBlockingMode moduleBlockingMode = _blockedModules.GetOrCreateValue(ecmaField.Module).BlockingMode;
            if (moduleBlockingMode == ModuleBlockingMode.None)
                return false;
            else if (moduleBlockingMode == ModuleBlockingMode.FullyBlocked)
                return true;

            // We are blocking internal implementation details
            Debug.Assert(moduleBlockingMode == ModuleBlockingMode.BlockedInternals);

            if (_blockedTypes.GetOrCreateValue((EcmaType)ecmaField.OwningType).IsBlocked)
                return true;

            if ((ecmaField.Attributes & FieldAttributes.Public) != FieldAttributes.Public)
                return true;

            return false;
        }
    }
}
