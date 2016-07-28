// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;

using Internal.LowLevelLinq;
using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;
using Internal.Reflection.Extensibility;
using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.CustomAttributes
{
    //
    // The Runtime's implementation of CustomAttributeData for normal metadata-based attributes
    //
    internal sealed class RuntimeNormalCustomAttributeData : RuntimeCustomAttributeData
    {
        internal RuntimeNormalCustomAttributeData(MetadataReader reader, CustomAttributeHandle customAttributeHandle)
        {
            _reader = reader;
            _customAttribute = customAttributeHandle.GetCustomAttribute(reader);
        }

        public sealed override Type AttributeType
        {
            get
            {
                Type lazyAttributeType = _lazyAttributeType;
                if (lazyAttributeType == null)
                {
                    lazyAttributeType = _lazyAttributeType = _customAttribute.GetAttributeTypeHandle(_reader).Resolve(_reader, new TypeContext(null, null)).CastToType();
                }
                return lazyAttributeType;
            }
        }

        internal sealed override String AttributeTypeString
        {
            get
            {
                return _customAttribute.GetAttributeTypeHandle(_reader).FormatTypeName(_reader, new TypeContext(null, null));
            }
        }

        //
        // If throwIfMissingMetadata is false, returns null rather than throwing a MissingMetadataException.
        //
        internal sealed override IList<CustomAttributeTypedArgument> GetConstructorArguments(bool throwIfMissingMetadata)
        {
            int index = 0;
            LowLevelList<Handle> lazyCtorTypeHandles = null;
            LowLevelListWithIList<CustomAttributeTypedArgument> customAttributeTypedArguments = new LowLevelListWithIList<CustomAttributeTypedArgument>();

            foreach (FixedArgumentHandle fixedArgumentHandle in _customAttribute.FixedArguments)
            {
                CustomAttributeTypedArgument customAttributeTypedArgument =
                    ParseFixedArgument(
                        _reader,
                        fixedArgumentHandle,
                        throwIfMissingMetadata,
                        delegate ()
                        {
                            // If we got here, the custom attribute blob lacked type information (this is actually the typical case.) We must fallback to
                            // parsing the constructor's signature to get the type info. 
                            if (lazyCtorTypeHandles == null)
                            {
                                IEnumerable<ParameterTypeSignatureHandle> parameterTypeSignatureHandles;
                                HandleType handleType = _customAttribute.Constructor.HandleType;
                                switch (handleType)
                                {
                                    case HandleType.QualifiedMethod:
                                        parameterTypeSignatureHandles = _customAttribute.Constructor.ToQualifiedMethodHandle(_reader).GetQualifiedMethod(_reader).Method.GetMethod(_reader).Signature.GetMethodSignature(_reader).Parameters;
                                        break;

                                    case HandleType.MemberReference:
                                        parameterTypeSignatureHandles = _customAttribute.Constructor.ToMemberReferenceHandle(_reader).GetMemberReference(_reader).Signature.ToMethodSignatureHandle(_reader).GetMethodSignature(_reader).Parameters;
                                        break;
                                    default:
                                        throw new BadImageFormatException();
                                }
                                LowLevelList<Handle> ctorTypeHandles = new LowLevelList<Handle>();
                                foreach (ParameterTypeSignatureHandle parameterTypeSignatureHandle in parameterTypeSignatureHandles)
                                {
                                    ctorTypeHandles.Add(parameterTypeSignatureHandle.GetParameterTypeSignature(_reader).Type);
                                }
                                lazyCtorTypeHandles = ctorTypeHandles;
                            }
                            Handle typeHandle = lazyCtorTypeHandles[index];
                            Exception exception = null;
                            RuntimeTypeInfo argumentType = typeHandle.TryResolve(_reader, new TypeContext(null, null), ref exception);
                            if (argumentType == null)
                            {
                                if (throwIfMissingMetadata)
                                    throw exception;
                                return null;
                            }
                            return argumentType;
                        }
                );

                if (customAttributeTypedArgument.ArgumentType == null)
                {
                    Debug.Assert(!throwIfMissingMetadata);
                    return null;
                }

                customAttributeTypedArguments.Add(customAttributeTypedArgument);
                index++;
            }

            return customAttributeTypedArguments;
        }

        //
        // If throwIfMissingMetadata is false, returns null rather than throwing a MissingMetadataException.
        //
        internal sealed override IList<CustomAttributeNamedArgument> GetNamedArguments(bool throwIfMissingMetadata)
        {
            LowLevelListWithIList<CustomAttributeNamedArgument> customAttributeNamedArguments = new LowLevelListWithIList<CustomAttributeNamedArgument>();
            foreach (NamedArgumentHandle namedArgumentHandle in _customAttribute.NamedArguments)
            {
                NamedArgument namedArgument = namedArgumentHandle.GetNamedArgument(_reader);
                String memberName = namedArgument.Name.GetString(_reader);
                bool isField = (namedArgument.Flags == NamedArgumentMemberKind.Field);
                CustomAttributeTypedArgument typedValue =
                    ParseFixedArgument(
                        _reader,
                        namedArgument.Value,
                        throwIfMissingMetadata,
                        delegate ()
                        {
                            // We got here because the custom attribute blob did not inclue type information. For named arguments, this is considered illegal metadata
                            // (ECMA always includes type info for named arguments.)
                            throw new BadImageFormatException();
                        }
                );
                if (typedValue.ArgumentType == null)
                {
                    Debug.Assert(!throwIfMissingMetadata);
                    return null;
                }
                customAttributeNamedArguments.Add(ExtensibleCustomAttributeData.CreateCustomAttributeNamedArgument(this.AttributeType, memberName, isField, typedValue));
            }
            return customAttributeNamedArguments;
        }

        // Equals/GetHashCode no need to override (they just implement reference equality but desktop never unified these things.)

        //
        // Helper for parsing custom attribute arguments.
        //
        // If throwIfMissingMetadata is false, returns default(CustomAttributeTypedArgument) rather than throwing a MissingMetadataException.
        //
        private CustomAttributeTypedArgument ParseFixedArgument(MetadataReader reader, FixedArgumentHandle fixedArgumentHandle, bool throwIfMissingMetadata, Func<RuntimeTypeInfo> getTypeFromConstructor)
        {
            FixedArgument fixedArgument = fixedArgumentHandle.GetFixedArgument(reader);
            RuntimeTypeInfo argumentType = null;
            if (fixedArgument.Type.IsNull(reader))
            {
                argumentType = getTypeFromConstructor();
                if (argumentType == null)
                {
                    Debug.Assert(!throwIfMissingMetadata);
                    return default(CustomAttributeTypedArgument);
                }
            }
            else
            {
                Exception exception = null;
                argumentType = fixedArgument.Type.TryResolve(reader, new TypeContext(null, null), ref exception);
                if (argumentType == null)
                {
                    if (throwIfMissingMetadata)
                        throw exception;
                    else
                        return default(CustomAttributeTypedArgument);
                }
            }

            Object value;
            Exception e = fixedArgument.Value.TryParseConstantValue(reader, out value);
            if (e != null)
            {
                if (throwIfMissingMetadata)
                    throw e;
                else
                    return default(CustomAttributeTypedArgument);
            }
            return WrapInCustomAttributeTypedArgument(value, argumentType.CastToType());
        }

        //
        // Wrap a custom attribute argument (or an element of an array-typed custom attribute argument) in a CustomAttributeTypeArgument structure
        // for insertion into a CustomAttributeData value.
        //
        private CustomAttributeTypedArgument WrapInCustomAttributeTypedArgument(Object value, Type argumentType)
        {
            if (argumentType.Equals(typeof(Object)))
            {
                // If the declared attribute type is System.Object, we must report the type based on the runtime value.
                if (value == null)
                    argumentType = typeof(String);  // Why is null reported as System.String? Because that's what the desktop CLR does.
                else if (value is Type)
                    argumentType = typeof(Type);    // value.GetType() will not actually be System.Type - rather it will be some internal implementation type. We only want to report it as System.Type.
                else
                    argumentType = value.GetType();
            }

            Array arrayValue = value as Array;
            if (arrayValue != null)
            {
                if (!argumentType.IsArray)
                    throw new BadImageFormatException();
                Type reportedElementType = argumentType.GetElementType();
                LowLevelListWithIList<CustomAttributeTypedArgument> elementTypedArguments = new LowLevelListWithIList<CustomAttributeTypedArgument>();
                foreach (Object elementValue in arrayValue)
                {
                    CustomAttributeTypedArgument elementTypedArgument = WrapInCustomAttributeTypedArgument(elementValue, reportedElementType);
                    elementTypedArguments.Add(elementTypedArgument);
                }
                return ExtensibleCustomAttributeData.CreateCustomAttributeTypedArgument(argumentType, new ReadOnlyCollection<CustomAttributeTypedArgument>(elementTypedArguments));
            }
            else
            {
                return ExtensibleCustomAttributeData.CreateCustomAttributeTypedArgument(argumentType, value);
            }
        }

        private readonly MetadataReader _reader;
        private readonly CustomAttribute _customAttribute;

        private volatile Type _lazyAttributeType;
    }
}
