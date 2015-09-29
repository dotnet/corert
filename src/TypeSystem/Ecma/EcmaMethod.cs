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

        MethodDefinition _methodDefinition;

        // Cached values
        MethodSignature _signature;
        MethodFlags _methodFlags;

        internal EcmaMethod(EcmaType type, MethodDefinitionHandle handle)
        {
            _type = type;
            _handle = handle;

            _methodDefinition = type.MetadataReader.GetMethodDefinition(handle);
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

        void ComputeSignature()
        {
            BlobReader signatureReader = this.MetadataReader.GetBlobReader(_methodDefinition.Signature);

            EcmaSignatureParser parser = new EcmaSignatureParser(this.Module, signatureReader);
            _signature = parser.ParseMethodSignature();
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                    ComputeSignature();
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

        public MethodDefinition MethodDefinition
        {
            get
            {
                return _methodDefinition;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private MethodFlags InitializeMethodFlags(MethodFlags mask)
        {
            MethodFlags flags = 0;

            if ((mask & MethodFlags.BasicMetadataCache) != 0)
            {
                var methodAttributes = _methodDefinition.Attributes;
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
                return _methodDefinition.Attributes;
            }
        }

        public MethodImplAttributes ImplAttributes
        {
            get
            {
                return _methodDefinition.ImplAttributes;
            }
        }

        public override string Name
        {
            get
            {
                return this.MetadataReader.GetString(_methodDefinition.Name);
            }
        }

        TypeDesc[] _genericParameters;

        public override Instantiation Instantiation
        {
            get
            {
                if (_genericParameters == null)
                {
                    var genericParameterHandles = _methodDefinition.GetGenericParameters();
                    int count = genericParameterHandles.Count;
                    if (count > 0)
                    {
                        TypeDesc[] genericParameters = new TypeDesc[count];
                        int i = 0;
                        foreach (var genericParameterHandle in genericParameterHandles)
                        {
                            genericParameters[i++] = new EcmaGenericParameter(this.Module, genericParameterHandle);
                        }
                        Interlocked.CompareExchange(ref _genericParameters, genericParameters, null);
                    }
                    else
                    {
                        _genericParameters = TypeDesc.EmptyTypes;
                    }
                }

                return new Instantiation(_genericParameters);
            }
        }

        public bool HasCustomAttribute(string customAttributeName)
        {
            return this.Module.HasCustomAttribute(_methodDefinition.GetCustomAttributes(), customAttributeName);
        }

        public override string ToString()
        {
            return "[" + Module.GetName().Name + "]" + _type.ToString() + "." + this.Name;
        }

        public bool IsPInvoke()
        {
            return (((int)Attributes & (int)MethodAttributes.PinvokeImpl) != 0);
        }

        public string GetPInvokeImportName()
        {
            if (((int)Attributes & (int)MethodAttributes.PinvokeImpl) == 0)
                return null;

            return this.MetadataReader.GetString(_methodDefinition.GetImport().Name);
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
