// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // Constructed generic types that don't have an EEType available.
    //
    internal sealed class RuntimeInspectionOnlyConstructedGenericType : RuntimeConstructedGenericType
    {
        internal RuntimeInspectionOnlyConstructedGenericType(RuntimeType genericTypeDefinition, RuntimeType[] genericTypeArguments)
            : base(new ConstructedGenericTypeKey(genericTypeDefinition, genericTypeArguments))
        {
            // We know this is a nop since just passed the key to our base class. However, we do want this subclass to follow
            // the PrepareKey() protocol without relying on knowledge about the base class so we'll go by the book and call it
            // anyway so that our GetHashCode() method is justified in not calling it again.
            this.PrepareKey();
        }

        public sealed override bool Equals(Object obj)
        {
            return InternalIsEqual(obj);  // Do not change this - see comments in RuntimeType.cs regarding Equals()
        }

        public sealed override int GetHashCode()
        {
            // Note: Ordinarily, we should invoke PrepareKey() first before calling Key. In this case, we called it during our constructor.
            return this.Key.GetHashCode();
        }

        //
        // Pay-for-play safe implementation of TypeInfo.ContainsGenericParameters()
        //
        public sealed override bool InternalIsOpen
        {
            get
            {
                foreach (RuntimeType genericTypeArgument in this.Key.GenericTypeArguments)
                {
                    if (genericTypeArgument.InternalIsOpen)
                        return true;
                }
                return false;
            }
        }

        protected sealed override String LastResortToString
        {
            get
            {
                return "";   // We never expect to get here since we always have full metadata.
            }
        }
    }
}




