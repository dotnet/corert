// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;
using global::System.Runtime.InteropServices;

using global::Internal.Runtime.Augments;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.Execution;
using global::Internal.Reflection.Execution.MethodInvokers;
using global::Internal.Reflection.Extensibility;

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution
{
    //==========================================================================================================
    // These ExecutionEnvironment entrypoints provide basic runtime allocation and policy services to
    // Reflection. Our implementation merely forwards to System.Private.CoreLib.
    //==========================================================================================================
    internal sealed partial class ExecutionEnvironmentImplementation : ExecutionEnvironment
    {
        public sealed override Object NewObject(RuntimeTypeHandle typeHandle)
        {
            return RuntimeAugments.NewObject(typeHandle);
        }

        public sealed override Array NewArray(RuntimeTypeHandle typeHandleForArrayType, int count)
        {
            return RuntimeAugments.NewArray(typeHandleForArrayType, count);
        }

        public sealed override Array NewMultiDimArray(RuntimeTypeHandle typeHandleForArrayType, int[] lengths, int[] lowerBounds)
        {
            return RuntimeAugments.NewMultiDimArray(typeHandleForArrayType, lengths, lowerBounds);
        }

        public sealed override RuntimeTypeHandle ProjectionTypeForArrays
        {
            get
            {
                return RuntimeAugments.ProjectionTypeForArrays;
            }
        }

        public sealed override bool IsAssignableFrom(RuntimeTypeHandle dstType, RuntimeTypeHandle srcType)
        {
            return RuntimeAugments.IsAssignableFrom(dstType, srcType);
        }

        public sealed override bool TryGetBaseType(RuntimeTypeHandle typeHandle, out RuntimeTypeHandle baseTypeHandle)
        {
            return RuntimeAugments.TryGetBaseType(typeHandle, out baseTypeHandle);
        }

        public sealed override IEnumerable<RuntimeTypeHandle> TryGetImplementedInterfaces(RuntimeTypeHandle typeHandle)
        {
            return RuntimeAugments.TryGetImplementedInterfaces(typeHandle);
        }

        public sealed override MethodInvoker GetSyntheticMethodInvoker(RuntimeTypeHandle thisType, RuntimeTypeHandle[] parameterTypes, InvokerOptions options, Func<Object, Object[], Object> invoker)
        {
            return new SyntheticMethodInvoker(thisType, parameterTypes, options, invoker);
        }

        public sealed override string GetLastResortString(RuntimeTypeHandle typeHandle)
        {
            return RuntimeAugments.GetLastResortString(typeHandle);
        }

        //==============================================================================================
        // Default Value support.
        //==============================================================================================
        public sealed override bool GetDefaultValueIfAny(MetadataReader reader, ParameterHandle parameterHandle, Type declaredType, IEnumerable<CustomAttributeData> customAttributes, out Object defaultValue)
        {
            Parameter parameter = parameterHandle.GetParameter(reader);
            return DefaultValueParser.GetDefaultValueIfAny(DefaultValueParser.MemberType.Parameter, reader, parameter.DefaultValue, declaredType, customAttributes, out defaultValue);
        }

        public sealed override bool GetDefaultValueIfAny(MetadataReader reader, FieldHandle fieldHandle, Type declaredType, IEnumerable<CustomAttributeData> customAttributes, out Object defaultValue)
        {
            Field field = fieldHandle.GetField(reader);
            return DefaultValueParser.GetDefaultValueIfAny(DefaultValueParser.MemberType.Field, reader, field.DefaultValue, declaredType, customAttributes, out defaultValue);
        }

        public sealed override bool GetDefaultValueIfAny(MetadataReader reader, PropertyHandle propertyHandle, Type declaredType, IEnumerable<CustomAttributeData> customAttributes, out Object defaultValue)
        {
            Property property = propertyHandle.GetProperty(reader);
            return DefaultValueParser.GetDefaultValueIfAny(DefaultValueParser.MemberType.Property, reader, property.DefaultValue, declaredType, customAttributes, out defaultValue);
        }

        //==============================================================================================
        // Pseudo Custom Attributes
        //==============================================================================================
        public sealed override IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, ScopeDefinitionHandle scopeDefinitionHandle)
        {
            return Empty<CustomAttributeData>.Enumerable;
        }

        public sealed override IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle)
        {
            TypeAttributes attributes = typeDefinitionHandle.GetTypeDefinition(reader).Flags;
            if (0 != (attributes & TypeAttributes.Import))
                yield return ReflectionCoreExecution.ExecutionDomain.GetCustomAttributeData(typeof(ComImportAttribute), null, null);
        }

        public sealed override IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, MethodHandle methodHandle, TypeDefinitionHandle declaringTypeHandle)
        {
            MethodImplAttributes implAttributes = methodHandle.GetMethod(reader).ImplFlags;
            if (0 != (implAttributes & MethodImplAttributes.PreserveSig))
                yield return ReflectionCoreExecution.ExecutionDomain.GetCustomAttributeData(typeof(PreserveSigAttribute), null, null);
        }

        public sealed override IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, ParameterHandle parameterHandle, MethodHandle declaringMethodHandle)
        {
            ParameterAttributes attributes = parameterHandle.GetParameter(reader).Flags;
            if (0 != (attributes & ParameterAttributes.In))
                yield return ReflectionCoreExecution.ExecutionDomain.GetCustomAttributeData(typeof(InAttribute), null, null);
            if (0 != (attributes & ParameterAttributes.Out))
                yield return ReflectionCoreExecution.ExecutionDomain.GetCustomAttributeData(typeof(OutAttribute), null, null);
            if (0 != (attributes & ParameterAttributes.Optional))
                yield return ReflectionCoreExecution.ExecutionDomain.GetCustomAttributeData(typeof(OptionalAttribute), null, null);
        }

        public sealed override IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, FieldHandle fieldHandle, TypeDefinitionHandle declaringTypeHandle)
        {
            TypeAttributes layoutKind = declaringTypeHandle.GetTypeDefinition(reader).Flags & TypeAttributes.LayoutMask;
            if (layoutKind == TypeAttributes.ExplicitLayout)
            {
                int offset = (int)(fieldHandle.GetField(reader).Offset);
                CustomAttributeTypedArgument offsetArgument = ExtensibleCustomAttributeData.CreateCustomAttributeTypedArgument(typeof(Int32), offset);
                yield return ReflectionCoreExecution.ExecutionDomain.GetCustomAttributeData(typeof(FieldOffsetAttribute), new CustomAttributeTypedArgument[] { offsetArgument }, null);
            }
        }

        public sealed override IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, PropertyHandle propertyHandle, TypeDefinitionHandle declaringTypeHandle)
        {
            return Empty<CustomAttributeData>.Enumerable;
        }

        public sealed override IEnumerable<CustomAttributeData> GetPsuedoCustomAttributes(MetadataReader reader, EventHandle eventHandle, TypeDefinitionHandle declaringTypeHandle)
        {
            return Empty<CustomAttributeData>.Enumerable;
        }
    }
}

