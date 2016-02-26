// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;

using global::Internal.Reflection.Core.NonPortable;
using Internal.Reflection.Tracing;
using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // TypeInfos that represent constructed generic types.
    // 
    //
    internal sealed partial class RuntimeConstructedGenericTypeInfo : RuntimeTypeInfo
    {
        private RuntimeConstructedGenericTypeInfo(RuntimeType genericConstructedGenericType)
            : base()
        {
            Debug.Assert(genericConstructedGenericType.IsConstructedGenericType);
            _asType = genericConstructedGenericType;
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_CustomAttributes(this);
#endif

                return GenericTypeDefinitionTypeInfo.CustomAttributes;
            }
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimeConstructedGenericTypeInfo other = obj as RuntimeConstructedGenericTypeInfo;
            if (other == null)
                return false;
            return _asType.Equals(other._asType);
        }

        public sealed override int GetHashCode()
        {
            return _asType.GetHashCode();
        }

        public sealed override Guid GUID
        {
            get
            {
                return GenericTypeDefinitionTypeInfo.GUID;
            }
        }

        public sealed override bool IsGenericType
        {
            get
            {
                return true;
            }
        }

        public sealed override Assembly Assembly
        {
            get
            {
                return GenericTypeDefinitionTypeInfo.Assembly;
            }
        }

        public sealed override TypeAttributes Attributes
        {
            get
            {
                return GenericTypeDefinitionTypeInfo.Attributes;
            }
        }

        public sealed override IEnumerable<TypeInfo> DeclaredNestedTypes
        {
            get
            {
                return GenericTypeDefinitionTypeInfo.DeclaredNestedTypes;
            }
        }

        //
        // Returns the anchoring typedef that declares the members that this type wants returned by the Declared*** properties.
        // The Declared*** properties will project the anchoring typedef's members by overriding their DeclaringType property with "this"
        // and substituting the value of this.TypeContext into any generic parameters.
        //
        // Default implementation returns null which causes the Declared*** properties to return no members.
        //
        // Note that this does not apply to DeclaredNestedTypes. Nested types and their containers have completely separate generic instantiation environments
        // (despite what C# might lead you to think.) Constructed generic types return the exact same same nested types that its generic type definition does
        // - i.e. their DeclaringTypes refer back to the generic type definition, not the constructed generic type.)
        //
        // Note also that we cannot use this anchoring concept for base types because of generic parameters. Generic parameters return
        // baseclass and interfaces based on its constraints.
        //
        internal sealed override RuntimeNamedTypeInfo AnchoringTypeDefinitionForDeclaredMembers
        {
            get
            {
                RuntimeTypeInfo genericTypeDefinition = this.GenericTypeDefinitionTypeInfo;
                RuntimeNamedTypeInfo genericTypeDefinitionNamedTypeInfo = genericTypeDefinition as RuntimeNamedTypeInfo;
                if (genericTypeDefinitionNamedTypeInfo == null)
                    throw this.ReflectionDomain.CreateMissingMetadataException(genericTypeDefinition);
                return genericTypeDefinitionNamedTypeInfo;
            }
        }

        internal sealed override RuntimeType RuntimeType
        {
            get
            {
                return _asType;
            }
        }

        //
        // Returns the base type as a typeDef, Ref, or Spec. Default behavior is to QTypeDefRefOrSpec.Null, which causes BaseType to return null.
        //
        internal sealed override QTypeDefRefOrSpec TypeRefDefOrSpecForBaseType
        {
            get
            {
                return this.GenericTypeDefinitionTypeInfo.TypeRefDefOrSpecForBaseType;
            }
        }

        //
        // Returns the *directly implemented* interfaces as typedefs, specs or refs. ImplementedInterfaces will take care of the transitive closure and
        // insertion of the TypeContext.
        //
        internal sealed override QTypeDefRefOrSpec[] TypeRefDefOrSpecsForDirectlyImplementedInterfaces
        {
            get
            {
                return this.GenericTypeDefinitionTypeInfo.TypeRefDefOrSpecsForDirectlyImplementedInterfaces;
            }
        }

        //
        // Returns the generic parameter substitutions to use when enumerating declared members, base class and implemented interfaces.
        //
        internal sealed override TypeContext TypeContext
        {
            get
            {
                return new TypeContext(this.RuntimeType.InternalRuntimeGenericTypeArguments, null);
            }
        }

        private RuntimeTypeInfo GenericTypeDefinitionTypeInfo
        {
            get
            {
                return GetGenericTypeDefinition().GetRuntimeTypeInfo<RuntimeTypeInfo>();
            }
        }

        private RuntimeType _asType;
    }
}

