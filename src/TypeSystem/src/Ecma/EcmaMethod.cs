// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;

using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    public sealed class EcmaMethod : MethodDesc
    {
        [Flags]
        enum MethodFlags
        {
            BasicMetadataCache = 0x01,
            Virtual = 0x02,
            NewSlot = 0x04,
        };

        EcmaType _type;
        MethodDefinitionHandle _handle;

        // Cached values
        MethodFlags _methodFlags;
        MethodSignature _signature;
        string _name;
        TypeDesc[] _genericParameters; // TODO: Optional field?

        internal EcmaMethod(EcmaType type, MethodDefinitionHandle handle)
        {
            _type = type;
            _handle = handle;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _type.Module.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _type;
            }
        }

        private MethodSignature InitializeSignature()
        {
            var metadataReader = MetadataReader;
            BlobReader signatureReader = metadataReader.GetBlobReader(metadataReader.GetMethodDefinition(_handle).Signature);

            EcmaSignatureParser parser = new EcmaSignatureParser(Module, signatureReader);
            var signature = parser.ParseMethodSignature();
            return (_signature = signature);
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                    return InitializeSignature();
                return _signature;
            }
        }
 
        public EcmaModule Module
        {
            get
            {
                return _type.Module;
            }
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _type.MetadataReader;
            }
        }

        public MethodDefinitionHandle Handle
        {
            get
            {
                return _handle;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private MethodFlags InitializeMethodFlags(MethodFlags mask)
        {
            MethodFlags flags = 0;

            if ((mask & MethodFlags.BasicMetadataCache) != 0)
            {
                var methodAttributes = Attributes;

                if ((methodAttributes & MethodAttributes.Virtual) != 0)
                    flags |= MethodFlags.Virtual;

                if ((methodAttributes & MethodAttributes.NewSlot) != 0)
                    flags |= MethodFlags.NewSlot;

                flags |= MethodFlags.BasicMetadataCache;
            }

            Debug.Assert((flags & mask) != 0);
            _methodFlags |= flags;

            return flags & mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MethodFlags GetMethodFlags(MethodFlags mask)
        {
            MethodFlags flags = _methodFlags & mask;
            if (flags != 0)
                return flags;
            return InitializeMethodFlags(mask);
        }

        public override bool IsVirtual
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.Virtual) & MethodFlags.Virtual) != 0;
            }
        }

        public override bool IsNewSlot
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.NewSlot) & MethodFlags.NewSlot) != 0;
            }
        }

        public MethodAttributes Attributes
        {
            get
            {
                return MetadataReader.GetMethodDefinition(_handle).Attributes;
            }
        }

        public MethodImplAttributes ImplAttributes
        {
            get
            {
                return MetadataReader.GetMethodDefinition(_handle).ImplAttributes;
            }
        }

        private string InitializeName()
        {
            var metadataReader = MetadataReader;
            var name = metadataReader.GetString(metadataReader.GetMethodDefinition(_handle).Name);
            return (_name = name);
        }

        public override string Name
        {
            get
            {
                if (_name == null)
                    return InitializeName();
                return _name;
            }
        }

        void ComputeGenericParameters()
        {
            var genericParameterHandles = MetadataReader.GetMethodDefinition(_handle).GetGenericParameters();
            int count = genericParameterHandles.Count;
            if (count > 0)
            {
                TypeDesc[] genericParameters = new TypeDesc[count];
                int i = 0;
                foreach (var genericParameterHandle in genericParameterHandles)
                {
                    genericParameters[i++] = new EcmaGenericParameter(Module, genericParameterHandle);
                }
                Interlocked.CompareExchange(ref _genericParameters, genericParameters, null);
            }
            else
            {
                _genericParameters = TypeDesc.EmptyTypes;
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                if (_genericParameters == null)
                    ComputeGenericParameters();
                return new Instantiation(_genericParameters);
            }
        }

        public bool HasCustomAttribute(string customAttributeName)
        {
            return Module.HasCustomAttribute(MetadataReader.GetMethodDefinition(_handle).GetCustomAttributes(), customAttributeName);
        }

        public override string ToString()
        {
            return _type.ToString() + "." + Name;
        }

        public bool IsPInvoke()
        {
            return (((int)Attributes & (int)MethodAttributes.PinvokeImpl) != 0);
        }

        public string GetPInvokeImportName()
        {
            if (((int)Attributes & (int)MethodAttributes.PinvokeImpl) == 0)
                return null;

            var metadataReader = MetadataReader;
            return metadataReader.GetString(metadataReader.GetMethodDefinition(_handle).GetImport().Name);
        }
    }

    public static class EcmaMethodExtensions
    {
        public static bool IsPublic(this EcmaMethod method) { return (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public; }
        public static bool IsPrivate(this EcmaMethod method) { return (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private; }
        public static bool IsStatic(this EcmaMethod method) { return (method.Attributes & MethodAttributes.Static) != 0; }
        public static bool IsFinal(this EcmaMethod method) { return (method.Attributes & MethodAttributes.Final) != 0; }
        public static bool IsHideBySig(this EcmaMethod method) { return (method.Attributes & MethodAttributes.HideBySig) != 0; }
        public static bool IsAbstract(this EcmaMethod method) { return (method.Attributes & MethodAttributes.Abstract) != 0; }
        public static bool IsSpecialName(this EcmaMethod method) { return (method.Attributes & MethodAttributes.SpecialName) != 0; }
    }
}
