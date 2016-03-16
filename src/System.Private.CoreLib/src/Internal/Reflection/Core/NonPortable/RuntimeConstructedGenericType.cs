// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Internal.Runtime.Augments;

#if ENABLE_REFLECTION_TRACE
using Internal.Reflection.Tracing;
#endif

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents open or closed constructed generic types (i.e. "Foo<int>" and "Foo<int, T>" but not "Foo<>").
    //
    // Note:
    //   The full CLR has an annoying quirk: if all of the generic type arguments are generic parameters declared by
    //   the generic type definition and they appear in the right order, the constructs get unified with the generic type definition itself.
    //
    //   For example,
    //
    //      class ListNode<T>
    //      {
    //           ListNode<T> _next;
    //      }
    //
    //   Reflection on the full CLR returns the same Type object for both:
    //
    //      typeof(ListNode<>)
    //
    //   and
    //
    //      Field next = typeof(ListNode()).GetTypeInfo().DeclaredFields.Single(f => f.Name == "_next");
    //      Type t = next.FieldType;
    //
    //      Assert(t == typeof(ListNode<>));                  // Full CLR passes this assert.
    //      Assert(!(t.IsConstructedGenericType));            // Full CLR passes this assert.
    //      Assert(t.GetTypeInfo().IsGenericTypeDefinition)); // Full CLR passes this assert.
    //
    //   For now, the POR is eliminate this quirk going forward. 
    //
    internal abstract class RuntimeConstructedGenericType : RuntimeType, IKeyedItem<ConstructedGenericTypeKey>
    {
        protected RuntimeConstructedGenericType()
            : base()
        {
        }

        protected RuntimeConstructedGenericType(ConstructedGenericTypeKey constructedGenericTypeKey)
            : base()
        {
            _lazyConstructedGenericTypeKeyHolder = new Tuple<ConstructedGenericTypeKey>(constructedGenericTypeKey);
        }

        public sealed override Type DeclaringType
        {
            get
            {
                ConstructedGenericTypeKey key = this.ConstructedGenericTypeKeyIfAvailable;
                if (key.IsAvailable)
                    return key.GenericTypeDefinition.DeclaringType;
                RuntimeTypeHandle runtimeTypeHandle;
                if (this.InternalTryGetTypeHandle(out runtimeTypeHandle) && RuntimeAugments.Callbacks.IsReflectionBlocked(runtimeTypeHandle))
                    return null;
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(this);
            }
        }

        public sealed override String FullName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_FullName(this);
#endif

                // Desktop quirk: open constructions don't have "fullNames".
                if (this.InternalIsOpen)
                    return null;
                String fullName = GetGenericTypeDefinition().FullName;
                fullName += "[";
                Type[] genericTypeArguments = GenericTypeArguments;
                for (int i = 0; i < genericTypeArguments.Length; i++)
                {
                    if (i != 0)
                        fullName += ",";
                    fullName += "[" + genericTypeArguments[i].AssemblyQualifiedName + "]";
                }
                fullName += "]";
                return fullName;
            }
        }

        public sealed override Type GetGenericTypeDefinition()
        {
            return ConstructedGenericTypeKey.GenericTypeDefinition;
        }

        public sealed override bool IsConstructedGenericType
        {
            get
            {
                return true;
            }
        }

        public sealed override String Namespace
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_Namespace(this);
#endif

                return GetGenericTypeDefinition().Namespace;
            }
        }

        public sealed override String ToString()
        {
            ConstructedGenericTypeKey key = this.ConstructedGenericTypeKeyIfAvailable;
            if (!key.IsAvailable)
                return this.LastResortToString;

            // Get the FullName of the generic type definition in a pay-for-play safe way.
            RuntimeType genericTypeDefinition = key.GenericTypeDefinition;
            String genericTypeDefinitionString = null;
            if (genericTypeDefinition.InternalNameIfAvailable != null)   // Want to avoid "cry-wolf" exceptions: if we can't even get the simple name, don't bother getting the FullName.
            {
                // Given our current pay for play policy, it should now be safe to attempt getting the FullName. (But guard with a try-catch in case this assumption is wrong.)
                try
                {
                    genericTypeDefinitionString = genericTypeDefinition.FullName;
                }
                catch (Exception)
                {
                }
            }
            // If all else fails, use the ToString() - it won't match the legacy CLR but with no metadata, we can't match it anyway.
            if (genericTypeDefinitionString == null)
                genericTypeDefinitionString = genericTypeDefinition.ToString();

            // Now, append the generic type arguments.
            String s = genericTypeDefinitionString;
            s += "[";
            Type[] genericTypeArguments = key.GenericTypeArguments;
            for (int i = 0; i < genericTypeArguments.Length; i++)
            {
                if (i != 0)
                    s += ",";
                s += genericTypeArguments[i].ToString();
            }
            s += "]";
            return s;
        }

        public sealed override String InternalGetNameIfAvailable(ref RuntimeType rootCauseForFailure)
        {
            ConstructedGenericTypeKey key = ConstructedGenericTypeKeyIfAvailable;
            if (!key.IsAvailable)
            {
                rootCauseForFailure = this;
                return null;
            }
            return key.GenericTypeDefinition.InternalGetNameIfAvailable(ref rootCauseForFailure);
        }


        public sealed override String InternalFullNameOfAssembly
        {
            get
            {
                return ConstructedGenericTypeKey.GenericTypeDefinition.InternalFullNameOfAssembly;
            }
        }

        public sealed override RuntimeType[] InternalRuntimeGenericTypeArguments
        {
            get
            {
                return ConstructedGenericTypeKey.GenericTypeArguments;
            }
        }

        //
        // Implements IKeyedItem.PrepareKey.
        // 
        // This method is the keyed item's chance to do any lazy evaluation needed to produce the key quickly. 
        // Concurrent unifiers are guaranteed to invoke this method at least once and wait for it
        // to complete before invoking the Key property. The unifier lock is NOT held across the call.
        //
        // PrepareKey() must be idempodent and thread-safe. It may be invoked multiple times and concurrently.
        // 
        public void PrepareKey()
        {
            // This is invoked for its side effect - ensures that the key is computable and latched.
            ConstructedGenericTypeKey key = ConstructedGenericTypeKey;
        }

        //
        // Implements IKeyedItem.Key.
        // 
        // Produce the key. This is a high-traffic property and is called while the hash table's lock is held. Thus, it should
        // return a precomputed stored value and refrain from invoking other methods. If the keyed item wishes to
        // do lazy evaluation of the key, it should do so in the PrepareKey() method.
        //
        public ConstructedGenericTypeKey Key
        {
            get
            {
                Debug.Assert(_lazyConstructedGenericTypeKeyHolder != null,
                    "IKeyedItem violation: Key invoked before PrepareKey().");
                return _lazyConstructedGenericTypeKeyHolder.Item1;
            }
        }

        //
        // If the subclass did not provide the key during construction, it must override this. We will invoke it during PrepareKey()
        // get the key in a delayed fashion.
        //
        protected virtual ConstructedGenericTypeKey CreateDelayedConstructedGenericTypeKeyIfAvailable()
        {
            Debug.Assert(false, "CreateDelayedConstructedGenericTypeKey() should not have been called since we provided the key during construction.");
            throw new NotSupportedException(); // Already gave you the key during construction!            
        }

        protected abstract String LastResortToString
        {
            get;
        }

        private ConstructedGenericTypeKey ConstructedGenericTypeKeyIfAvailable
        {
            get
            {
                Tuple<ConstructedGenericTypeKey> lazyConstructedGenericTypeKeyHolder = _lazyConstructedGenericTypeKeyHolder;
                if (lazyConstructedGenericTypeKeyHolder == null)
                {
                    ConstructedGenericTypeKey key = CreateDelayedConstructedGenericTypeKeyIfAvailable();
                    if (key.IsAvailable)
                    {
                        _lazyConstructedGenericTypeKeyHolder = new Tuple<ConstructedGenericTypeKey>(key);
                    }
                    return key;
                }
                return lazyConstructedGenericTypeKeyHolder.Item1;
            }
        }

        private ConstructedGenericTypeKey ConstructedGenericTypeKey
        {
            get
            {
                ConstructedGenericTypeKey key = this.ConstructedGenericTypeKeyIfAvailable;
                if (!key.IsAvailable)
                    throw RuntimeAugments.Callbacks.CreateMissingMetadataException(this);
                return Key;
            }
        }

        private volatile Tuple<ConstructedGenericTypeKey> _lazyConstructedGenericTypeKeyHolder;
    }
}


