// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;
using Internal.Reflection.Extensibility;

namespace Internal.Reflection.Core.NonPortable
{
    [DebuggerDisplay("{_debugName}")]
    public class RuntimeType : ExtensibleType, IEquatable<RuntimeType>
    {
        private EETypePtr _pEEType;

        public RuntimeType()
        {
            throw new NotImplementedException(); // CORERT-TODO: RuntimeType
        }

        internal RuntimeType(EETypePtr pEEType)
            : base()
        {
            _pEEType = pEEType;
        }

        internal EETypePtr ToEETypePtr()
        {
            return _pEEType;
        }

        public override bool Equals(Object obj)
        {
            return Object.ReferenceEquals(this, obj);
        }

        public bool Equals(RuntimeType obj)
        {
            return Object.ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return _pEEType.GetHashCode();
        }

        public sealed override String AssemblyQualifiedName
        {
            get
            {
                String fullName = FullName;
                if (fullName == null)   // Some Types (such as generic parameters) return null for FullName by design.
                    return null;

                String assemblyName = InternalFullNameOfAssembly;
                return fullName + ", " + assemblyName;
            }
        }

        //
        // Left unsealed as nested and generic parameter types must override this.
        //
        public override Type DeclaringType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        //
        // Left unsealed as this is only correct for named types. Other type flavors must override this.
        //
        public override String FullName
        {
            get
            {
                Debug.Assert(!IsConstructedGenericType);
                Debug.Assert(!IsGenericParameter);
                Debug.Assert(!HasElementType);

                String name = Name;

                Type declaringType = this.DeclaringType;
                if (declaringType != null)
                {
                    String declaringTypeFullName = declaringType.FullName;
                    return declaringTypeFullName + "+" + name;
                }

                String ns = Namespace;
                if (ns == null)
                    return name;
                return ns + "." + name;
            }
        }

        //
        // Left unsealed as generic parameter types must override this.
        //
        public override int GenericParameterPosition
        {
            get
            {
                Debug.Assert(!IsGenericParameter);
                throw new InvalidOperationException(SR.Arg_NotGenericParameter);
            }
        }

        public sealed override Type[] GenericTypeArguments
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public override bool IsArray
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public override bool IsByRef
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public override bool IsConstructedGenericType
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public override bool IsGenericParameter
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public override bool IsPointer
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }


        //
        // Left unsealed as array types must override this.
        //
        public override int GetArrayRank()
        {
            throw new NotImplementedException(); // CORERT-TODO: RuntimeType
        }

        public override Type GetElementType()
        {
            throw new NotImplementedException(); // CORERT-TODO: RuntimeType
        }

        //
        // Left unsealed as IsGenericType types must override this.
        //
        public override Type GetGenericTypeDefinition()
        {
            throw new NotImplementedException(); // CORERT-TODO: RuntimeType
        }

        public sealed override Type MakeArrayType()
        {
            // Do not implement this as a call to MakeArrayType(1) - they are not interchangable. MakeArrayType() returns a
            // vector type ("SZArray") while MakeArrayType(1) returns a multidim array of rank 1. These are distinct types
            // in the ECMA model and in CLR Reflection.
            return ReflectionCoreNonPortable.GetArrayType(this);
        }

        public sealed override Type MakeArrayType(int rank)
        {
            if (rank <= 0)
                throw new IndexOutOfRangeException();
            return ReflectionCoreNonPortable.GetMultiDimArrayType(this, rank);
        }

        public sealed override Type MakeByRefType()
        {
            return ReflectionCoreNonPortable.GetByRefType(this);
        }

        public sealed override Type MakeGenericType(params Type[] instantiation)
        {
            if (instantiation == null)
                throw new ArgumentNullException("instantiation");

            if (!(this.InternalIsGenericTypeDefinition))
                throw new InvalidOperationException(SR.Format(SR.Arg_NotGenericTypeDefinition, this));

            // We intentionally don't validate the number of arguments or their suitability to the generic type's constraints.
            // In a pay-for-play world, this can cause needless MissingMetadataExceptions. There is no harm in creating
            // the Type object for an inconsistent generic type - no EEType will ever match it so any attempt to "invoke" it
            // will throw an exception.
            RuntimeType[] genericTypeArguments = new RuntimeType[instantiation.Length];
            for (int i = 0; i < instantiation.Length; i++)
            {
                genericTypeArguments[i] = instantiation[i] as RuntimeType;
                if (genericTypeArguments[i] == null)
                {
                    if (instantiation[i] == null)
                        throw new ArgumentNullException();
                    else
                        throw new PlatformNotSupportedException(SR.PlatformNotSupported_MakeGenericType); // "PlatformNotSupported" because on desktop, passing in a foreign type is allowed and creates a RefEmit.TypeBuilder
                }
            }
            return ReflectionCoreNonPortable.GetConstructedGenericType(this, genericTypeArguments);
        }

        public sealed override Type MakePointerType()
        {
            return ReflectionCoreNonPortable.GetPointerType(this);
        }

        public override String Name
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public override String Namespace
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public override RuntimeTypeHandle TypeHandle
        {
            get
            {
                return new RuntimeTypeHandle(this);
            }
        }

        public override String ToString()
        {
            throw new NotImplementedException(); // CORERT-TODO: RuntimeType
        }

        public bool InternalTryGetTypeHandle(out RuntimeTypeHandle typeHandle)
        {
            typeHandle = new RuntimeTypeHandle(this);
            return true;
        }

        public virtual bool InternalIsGenericTypeDefinition
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public virtual bool InternalIsOpen
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public String InternalNameIfAvailable
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public virtual String InternalFullNameOfAssembly
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public virtual bool InternalViolatesTypeIdentityRules
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public virtual String InternalGetNameIfAvailable(ref RuntimeType rootCauseForFailure)
        {
            throw new NotImplementedException(); // CORERT-TODO: RuntimeType
        }

        public virtual RuntimeType InternalRuntimeElementType
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public virtual RuntimeType[] InternalRuntimeGenericTypeArguments
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public virtual bool InternalIsMultiDimArray
        {
            get
            {
                throw new NotImplementedException(); // CORERT-TODO: RuntimeType
            }
        }

        public virtual bool InternalIsEqual(Object obj)
        {
            throw new NotImplementedException(); // CORERT-TODO: RuntimeType
        }

        public RUNTIMETYPEINFO InternalGetLatchedRuntimeTypeInfo<RUNTIMETYPEINFO>(Func<RuntimeType, RUNTIMETYPEINFO> factory) where RUNTIMETYPEINFO : class
        {
            throw new NotImplementedException(); // CORERT-TODO: RuntimeType
        }

        //
        // Note: This can be (and is) called multiple times. We do not do this work in the constructor as calling ToString()
        // in the constructor causes some serious recursion issues.
        //
        public void EstablishDebugName()
        {
            bool populateDebugNames = DeveloperExperienceState.DeveloperExperienceModeEnabled;
#if DEBUG
            populateDebugNames = true;
#endif
            if (!populateDebugNames)
                return;

            if (_debugName == null)
            {
                _debugName = "Constructing..."; // Protect against any inadvertent reentrancy.
                String debugName = this.ToString();
                if (debugName == null)
                    debugName = "";
                _debugName = debugName;
            }
            return;
        }

        private String _debugName;
    }
}
