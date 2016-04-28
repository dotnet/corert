// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// Manages policies around static constructors (.cctors) and static data initialization.
    /// </summary>
    public class TypeInitialization
    {
        // Eventually, this class will also manage preinitialization (interpreting cctors at compile
        // time and converting them to blobs of preinitialized data), and the various
        // System.Runtime.CompilerServices.PreInitializedAttribute/InitDataBlobAttribute/etc. placed on
        // types and their members by toolchain components.

        /// <summary>
        /// Returns true if '<paramref name="type"/>' has a lazily executed static constructor.
        /// A lazy static constructor gets executed on first access to type's members.
        /// </summary>
        public bool HasLazyStaticConstructor(TypeDesc type)
        {
            return type.HasStaticConstructor && !HasEagerConstructorAttribute(type);
        }

        /// <summary>
        /// Returns true if '<paramref name="type"/>' has a static constructor that is eagerly
        /// executed at process startup time.
        /// </summary>
        public bool HasEagerStaticConstructor(TypeDesc type)
        {
            return type.HasStaticConstructor && HasEagerConstructorAttribute(type);
        }

        private static bool HasEagerConstructorAttribute(TypeDesc type)
        {
            MetadataType mdType = type as MetadataType;
            return mdType != null && (
                mdType.HasCustomAttribute("System.Runtime.CompilerServices", "EagerOrderedStaticConstructorAttribute")
                || mdType.HasCustomAttribute("System.Runtime.CompilerServices", "EagerStaticClassConstructionAttribute"));
        }
    }

    public class EagerConstructorComparer : IComparer<DependencyAnalysis.IMethodNode>
    {
        private int GetConstructionOrder(MetadataType type)
        {
            // For EagerOrderedStaticConstructorAttribute, order is defined by an integer.
            // For the other case (EagerStaticClassConstructionAttribute), order is defined
            // implicitly.

            var decoded = ((EcmaType)type.GetTypeDefinition()).GetDecodedCustomAttribute(
                "System.Runtime.CompilerServices", "EagerOrderedStaticConstructorAttribute");

            if (decoded != null)
                return (int)decoded.Value.FixedArguments[0].Value;

            Debug.Assert(type.HasCustomAttribute("System.Runtime.CompilerServices", "EagerStaticClassConstructionAttribute"));
            // RhBind on .NET Native for UWP will sort these based on static dependencies of the .cctors.
            // We could probably do the same, but this attribute is pretty much deprecated in favor of
            // EagerOrderedStaticConstructorAttribute that has explicit order. The remaining uses of
            // the unordered one don't appear to have dependencies, so sorting them all before the
            // ordered ones should do.
            return -1;
        }

        public int Compare(DependencyAnalysis.IMethodNode x, DependencyAnalysis.IMethodNode y)
        {
            var typeX = (MetadataType)x.Method.OwningType;
            var typeY = (MetadataType)y.Method.OwningType;

            int orderX = GetConstructionOrder(typeX);
            int orderY = GetConstructionOrder(typeY);

            int result;
            if (orderX != orderY)
            {
                result = Comparer<int>.Default.Compare(orderX, orderY);
            }
            else
            {
                // Use type name as a tie breaker. We need this algorithm to produce stable
                // ordering so that the sequence of eager cctors is deterministic.
                result = String.Compare(typeX.GetFullName(), typeY.GetFullName(), StringComparison.Ordinal);
            }
            
            return result;
        }
    }
}
