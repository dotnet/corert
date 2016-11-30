// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using Internal.TypeSystem;
using Internal.Runtime;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;
using System.Text;
using Debug = System.Diagnostics.Debug;
using Internal.NativeFormat;

namespace Internal.TypeSystem.NoMetadata
{
    /// <summary>
    /// Type that once had metadata, but that metadata is not available
    /// for the lifetime of the TypeSystemContext. Directly correlates
    /// to a RuntimeTypeHandle useable in the current environment.
    /// This type replaces the placeholder NoMetadataType that comes
    /// with the common type system codebase
    /// </summary>
    internal class NoMetadataType : DefType
    {
        private TypeSystemContext _context;
        private int _hashcode;
        private RuntimeTypeHandle _genericTypeDefinition;
        private DefType _genericTypeDefinitionAsDefType;
        private Instantiation _instantiation;
        private bool _baseTypeCached;
        private DefType _baseType;

        public NoMetadataType(TypeSystemContext context, RuntimeTypeHandle genericTypeDefinition, DefType genericTypeDefinitionAsDefType, Instantiation instantiation, int hashcode)
        {
            _hashcode = hashcode;
            _context = context;
            _genericTypeDefinition = genericTypeDefinition;
            _genericTypeDefinitionAsDefType = genericTypeDefinitionAsDefType;
            if (_genericTypeDefinitionAsDefType == null)
                _genericTypeDefinitionAsDefType = this;
            _instantiation = instantiation;

            // Instantiation must either be:
            // Something valid (if the type is generic, or a generic type definition)
            // or Empty (if the type isn't a generic of any form)
            unsafe
            {
                Debug.Assert(((_instantiation.Length > 0) && _genericTypeDefinition.ToEETypePtr()->IsGenericTypeDefinition) ||
                             ((_instantiation.Length == 0) && !_genericTypeDefinition.ToEETypePtr()->IsGenericTypeDefinition));
            }
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _context;
            }
        }

        public override DefType BaseType
        {
            get
            {
                if (_baseTypeCached)
                    return _baseType;

                if (RetrieveRuntimeTypeHandleIfPossible())
                {
                    RuntimeTypeHandle baseTypeHandle;
                    if (!RuntimeAugments.TryGetBaseType(RuntimeTypeHandle, out baseTypeHandle))
                    {
                        Debug.Assert(false);
                    }

                    DefType baseType = !baseTypeHandle.IsNull() ? (DefType)Context.ResolveRuntimeTypeHandle(baseTypeHandle) : null;
                    SetBaseType(baseType);

                    return baseType;
                }
                else
                {
                    Debug.Assert(this.HasNativeLayout);
                    // Parsing of the base type has not yet happened. Perform that part of native layout parsing
                    // just-in-time
                    TypeBuilderState state = GetOrCreateTypeBuilderState();

                    ComputeTemplate();
                    NativeParser typeInfoParser = state.GetParserForNativeLayoutInfo();
                    NativeParser baseTypeParser = typeInfoParser.GetParserForBagElementKind(BagElementKind.BaseType);

                    ParseBaseType(state.NativeLayoutInfo.LoadContext, baseTypeParser);
                    Debug.Assert(_baseTypeCached);
                    return _baseType;
                }
            }
        }

        internal override void ParseBaseType(NativeLayoutInfoLoadContext nativeLayoutInfoLoadContext, NativeParser baseTypeParser)
        {
            if (!baseTypeParser.IsNull)
            {
                // If the base type is available from the native layout info use it if the type we have is a NoMetadataType
                SetBaseType((DefType)nativeLayoutInfoLoadContext.GetType(ref baseTypeParser));
            }
            else
            {
                // Set the base type for no metadata types, if we reach this point, and there isn't a parser, then we simply use the value from the template
                SetBaseType(ComputeTemplate().BaseType);
            }
        }

        /// <summary>
        /// This is used to set base type for generic types without metadata
        /// </summary>
        public void SetBaseType(DefType baseType)
        {
            Debug.Assert(!_baseTypeCached || _baseType == baseType);
            _baseType = baseType;
            _baseTypeCached = true;
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = 0;

            if ((mask & TypeFlags.ContainsGenericVariablesComputed) != 0)
            {
                flags |= TypeFlags.ContainsGenericVariablesComputed;

                for (int i = 0; i < _instantiation.Length; i++)
                {
                    if (_instantiation[i].ContainsGenericVariables)
                    {
                        flags |= TypeFlags.ContainsGenericVariables;
                        break;
                    }
                }
            }

            if ((mask & TypeFlags.CategoryMask) != 0)
            {
                unsafe
                {
                    EEType* eetype = _genericTypeDefinition.ToEETypePtr();
                    if (eetype->IsValueType)
                    {
                        if (eetype->CorElementType == 0)
                        {
                            flags |= TypeFlags.ValueType;
                        }
                        else
                        {
                            if (eetype->BaseType == typeof(System.Enum).TypeHandle.ToEETypePtr())
                            {
                                flags |= TypeFlags.Enum;
                            }
                            else
                            {
                                // Primitive type.
                                if (eetype->CorElementType <= CorElementType.ELEMENT_TYPE_U8)
                                {
                                    flags |= (TypeFlags)eetype->CorElementType;
                                }
                                else
                                {
                                    switch (eetype->CorElementType)
                                    {
                                        case CorElementType.ELEMENT_TYPE_I:
                                            flags |= TypeFlags.IntPtr;
                                            break;

                                        case CorElementType.ELEMENT_TYPE_U:
                                            flags |= TypeFlags.UIntPtr;
                                            break;

                                        case CorElementType.ELEMENT_TYPE_R4:
                                            flags |= TypeFlags.Single;
                                            break;

                                        case CorElementType.ELEMENT_TYPE_R8:
                                            flags |= TypeFlags.Double;
                                            break;

                                        default:
                                            throw new BadImageFormatException();
                                    }
                                }
                            }
                        }
                    }
                    else if (eetype->IsInterface)
                    {
                        flags |= TypeFlags.Interface;
                    }
                    else
                    {
                        flags |= TypeFlags.Class;
                    }
                }
            }

            return flags;
        }

        // Canonicalization handling

        public override bool IsCanonicalSubtype(CanonicalFormKind policy)
        {
            foreach (TypeDesc t in Instantiation)
            {
                if (t.IsCanonicalSubtype(policy))
                {
                    return true;
                }
            }

            return false;
        }

        protected override TypeDesc ConvertToCanonFormImpl(CanonicalFormKind kind)
        {
            bool needsChange;
            Instantiation canonInstantiation = Context.ConvertInstantiationToCanonForm(Instantiation, kind, out needsChange);
            if (needsChange)
            {
                TypeDesc openType = GetTypeDefinition();
                return openType.InstantiateSignature(canonInstantiation, new Instantiation());
            }

            return this;
        }

        public override TypeDesc GetTypeDefinition()
        {
            if (_genericTypeDefinitionAsDefType != null)
                return _genericTypeDefinitionAsDefType;
            else
                return this;
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc[] clone = null;

            for (int i = 0; i < _instantiation.Length; i++)
            {
                TypeDesc uninst = _instantiation[i];
                TypeDesc inst = uninst.InstantiateSignature(typeInstantiation, methodInstantiation);
                if (inst != uninst)
                {
                    if (clone == null)
                    {
                        clone = new TypeDesc[_instantiation.Length];
                        for (int j = 0; j < clone.Length; j++)
                        {
                            clone[j] = _instantiation[j];
                        }
                    }
                    clone[i] = inst;
                }
            }

            return (clone == null) ? this : _genericTypeDefinitionAsDefType.Context.ResolveGenericInstantiation(_genericTypeDefinitionAsDefType, new Instantiation(clone));
        }

        public override Instantiation Instantiation
        {
            get
            {
                return _instantiation;
            }
        }

        public override TypeDesc UnderlyingType
        {
            get
            {
                if (!this.IsEnum)
                    return this;

                unsafe
                {
                    CorElementType corElementType = RuntimeTypeHandle.ToEETypePtr()->CorElementType;

                    return Context.GetTypeFromCorElementType(corElementType);
                }
            }
        }

#if DEBUG
        private string _cachedToString = null;

        public override string ToString()
        {
            if (_cachedToString != null)
                return _cachedToString;

            StringBuilder sb = new StringBuilder();

            if (!_genericTypeDefinition.IsNull())
                sb.Append(_genericTypeDefinition.LowLevelToString());
            else if (!RuntimeTypeHandle.IsNull())
                sb.Append(RuntimeTypeHandle.LowLevelToString());

            if (!Instantiation.IsNull)
            {
                for (int i = 0; i < Instantiation.Length; i++)
                {
                    sb.Append(i == 0 ? "[" : ", ");
                    sb.Append(Instantiation[i].ToString());
                }
                if (Instantiation.Length > 0) sb.Append("]");
            }

            _cachedToString = sb.ToString();

            return _cachedToString;
        }
#endif
    }
}
