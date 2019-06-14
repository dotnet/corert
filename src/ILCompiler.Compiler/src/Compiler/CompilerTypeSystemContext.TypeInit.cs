// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    // Manages policies around static constructors (.cctors) and static data initialization.
    partial class CompilerTypeSystemContext
    {
        // Eventually, this will also manage preinitialization (interpreting cctors at compile
        // time and converting them to blobs of preinitialized data), and the various
        // System.Runtime.CompilerServices.PreInitializedAttribute/InitDataBlobAttribute/etc. placed on
        // types and their members by toolchain components.

        /// <summary>
        /// Returns true if '<paramref name="type"/>' has a lazily executed static constructor.
        /// A lazy static constructor gets executed on first access to type's members.
        /// </summary>
        public bool HasLazyStaticConstructor(TypeDesc type)
        {
            return type.HasStaticConstructor && !HasEagerConstructorAttribute(type) && _supportsLazyCctors &&
                (!(type is MetadataType) || !((MetadataType)type).IsModuleType);
        }

        /// <summary>
        /// Returns true if '<paramref name="type"/>' has a static constructor that is eagerly
        /// executed at process startup time.
        /// </summary>
        public bool HasEagerStaticConstructor(TypeDesc type)
        {
            return type.HasStaticConstructor && (HasEagerConstructorAttribute(type) || !_supportsLazyCctors);
        }

        private static bool HasEagerConstructorAttribute(TypeDesc type)
        {
            MetadataType mdType = type as MetadataType;
            return mdType != null && 
                mdType.HasCustomAttribute("System.Runtime.CompilerServices", "EagerStaticClassConstructionAttribute");
        }
    }
}
