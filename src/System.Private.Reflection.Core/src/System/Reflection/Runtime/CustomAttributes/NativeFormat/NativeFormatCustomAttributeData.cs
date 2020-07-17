// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeInfos.NativeFormat;
using System.Reflection.Runtime.MethodInfos;
using System.Reflection.Runtime.MethodInfos.NativeFormat;

using Internal.LowLevelLinq;
using Internal.Reflection.Core;
using Internal.Reflection.Augments;
using Internal.Reflection.Core.Execution;
using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.CustomAttributes.NativeFormat
{
    //
    // The Runtime's implementation of CustomAttributeData for normal metadata-based attributes
    //
    internal sealed class NativeFormatCustomAttributeData : RuntimeCustomAttributeData
    {
        internal NativeFormatCustomAttributeData(MetadataReader reader, CustomAttributeHandle customAttributeHandle)
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
                    lazyAttributeType = _lazyAttributeType = _customAttribute.GetAttributeTypeHandle(_reader).Resolve(_reader, new TypeContext(null, null));
                }
                return lazyAttributeType;
            }
        }

        public sealed override ConstructorInfo Constructor
        {
            get
            {
                MetadataReader reader = _reader;
                HandleType constructorHandleType = _customAttribute.Constructor.HandleType;

                if (constructorHandleType == HandleType.QualifiedMethod)
                {
                    QualifiedMethod qualifiedMethod = _customAttribute.Constructor.ToQualifiedMethodHandle(reader).GetQualifiedMethod(reader);
                    TypeDefinitionHandle declaringType = qualifiedMethod.EnclosingType;
                    MethodHandle methodHandle = qualifiedMethod.Method;
                    NativeFormatRuntimeNamedTypeInfo attributeType = NativeFormatRuntimeNamedTypeInfo.GetRuntimeNamedTypeInfo(reader, declaringType, default(RuntimeTypeHandle));
                    return RuntimePlainConstructorInfo<NativeFormatMethodCommon>.GetRuntimePlainConstructorInfo(new NativeFormatMethodCommon(methodHandle, attributeType, attributeType));
                }
                else if (constructorHandleType == HandleType.MemberReference)
                {
                    MemberReference memberReference = _customAttribute.Constructor.ToMemberReferenceHandle(reader).GetMemberReference(reader);

                    // There is no chance a custom attribute type will be an open type specification so we can safely pass in the empty context here.
                    TypeContext typeContext = new TypeContext(Array.Empty<RuntimeTypeInfo>(), Array.Empty<RuntimeTypeInfo>());
                    RuntimeTypeInfo attributeType = memberReference.Parent.Resolve(reader, typeContext);
                    MethodSignature sig = memberReference.Signature.ParseMethodSignature(reader);
                    HandleCollection parameters = sig.Parameters;
                    int numParameters = parameters.Count;
                    if (numParameters == 0)
                        return ResolveAttributeConstructor(attributeType, Array.Empty<Type>());

                    Type[] expectedParameterTypes = new Type[numParameters];
                    int index = 0;
                    foreach (Handle _parameterHandle in parameters)
                    {
                        Handle parameterHandle = _parameterHandle;
                        expectedParameterTypes[index++] = parameterHandle.Resolve(reader, attributeType.TypeContext);
                    }
                    return ResolveAttributeConstructor(attributeType, expectedParameterTypes);
                }
                else
                {
                    throw new BadImageFormatException();
                }
            }
        }

        internal sealed override String AttributeTypeString
        {
            get
            {
                return new QTypeDefRefOrSpec(_reader, _customAttribute.GetAttributeTypeHandle(_reader)).FormatTypeName(new TypeContext(null, null));
            }
        }

        //
        // If throwIfMissingMetadata is false, returns null rather than throwing a MissingMetadataException.
        //
        internal sealed override IList<CustomAttributeTypedArgument> GetConstructorArguments(bool throwIfMissingMetadata)
        {
            int index = 0;
            Handle[] lazyCtorTypeHandles = null;
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
                                HandleCollection parameterTypeSignatureHandles;
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
                                lazyCtorTypeHandles = parameterTypeSignatureHandles.ToArray();
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
                customAttributeNamedArguments.Add(ReflectionAugments.CreateCustomAttributeNamedArgument(this.AttributeType, memberName, isField, typedValue));
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
            return WrapInCustomAttributeTypedArgument(value, argumentType);
        }

        private readonly MetadataReader _reader;
        private readonly CustomAttribute _customAttribute;

        private volatile Type _lazyAttributeType;
    }
}
