// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.Runtime.Augments;

using Internal.Reflection.Core.Execution;
using Internal.Reflection.Execution.FieldAccessors;

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

        public sealed override string GetLastResortString(RuntimeTypeHandle typeHandle)
        {
            return RuntimeAugments.GetLastResortString(typeHandle);
        }

        //==============================================================================================
        // Miscellaneous
        //==============================================================================================
        public sealed override FieldAccessor CreateLiteralFieldAccessor(object value, RuntimeTypeHandle fieldTypeHandle)
        {
            return new LiteralFieldAccessor(value, fieldTypeHandle); 
        }
    }
}

