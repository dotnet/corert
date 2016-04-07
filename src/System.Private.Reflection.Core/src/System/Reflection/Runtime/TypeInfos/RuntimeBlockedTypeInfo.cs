// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.Assemblies;
using global::System.Reflection.Runtime.CustomAttributes;

using global::Internal.LowLevelLinq;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // TypeInfos that represent type definitions (i.e. Foo or Foo<>) or constructed generic types (Foo<int>)
    // that can never be reflection-enabled due to the framework Reflection block.
    //
    // These types differ from NoMetadata TypeInfos in that properties that inquire about members,
    // custom attributes or interfaces return an empty list rather than throwing a MissingMetadataException.
    //
    // Since these represent "internal framework types", the app cannot prove we are lying.
    // 
    internal sealed partial class RuntimeBlockedTypeInfo : RuntimeTypeInfo
    {
        private RuntimeBlockedTypeInfo(RuntimeType runtimeType)
        {
            _asType = runtimeType;
        }

        public sealed override Assembly Assembly
        {
            get
            {
                return typeof(Object).GetTypeInfo().Assembly;
            }
        }

        public sealed override TypeAttributes Attributes
        {
            get
            {
                return TypeAttributes.Class | TypeAttributes.NotPublic;
            }
        }

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                return Empty<CustomAttributeData>.Enumerable;
            }
        }

        public sealed override IEnumerable<TypeInfo> DeclaredNestedTypes
        {
            get
            {
                return Empty<TypeInfo>.Enumerable;
            }
        }

        public sealed override bool Equals(Object obj)
        {
            if (Object.ReferenceEquals(this, obj))
                return true;

            RuntimeBlockedTypeInfo other = obj as RuntimeBlockedTypeInfo;
            if (other == null)
                return false;
            if (!(this._asType.Equals(other._asType)))
                return false;
            return true;
        }

        public sealed override int GetHashCode()
        {
            return _asType.GetHashCode();
        }

        public sealed override Guid GUID
        {
            get
            {
                throw this.ReflectionDomain.CreateMissingMetadataException(this);
            }
        }

        public sealed override bool IsGenericType
        {
            get
            {
                return _asType.IsConstructedGenericType || this.IsGenericTypeDefinition;
            }
        }

        public sealed override bool IsGenericTypeDefinition
        {
            get
            {
                return _asType.InternalIsGenericTypeDefinition;
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
                return null;  // this causes the type to report having no members.
            }
        }

        internal sealed override RuntimeType[] RuntimeGenericTypeParameters
        {
            get
            {
                throw this.ReflectionDomain.CreateMissingMetadataException(this);
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
                throw this.ReflectionDomain.CreateMissingMetadataException(this);
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
                throw this.ReflectionDomain.CreateMissingMetadataException(this);
            }
        }

        //
        // Returns the generic parameter substitutions to use when enumerating declared members, base class and implemented interfaces.
        //
        internal sealed override TypeContext TypeContext
        {
            get
            {
                throw this.ReflectionDomain.CreateMissingMetadataException(this);
            }
        }

        private RuntimeType _asType;
    }
}



