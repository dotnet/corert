// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.IL;
using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Manages policies around static constructors (.cctors) and static data initialization. 
    /// </summary>
    public class PreinitializationManager
    {
        private readonly bool _supportsLazyCctors;
        private readonly CompilationModuleGroup _compilationModuleGroup;
        private readonly ILProvider _ilprovider;

        public PreinitializationManager(TypeSystemContext context, CompilationModuleGroup compilationGroup, ILProvider ilprovider)
        {
            _supportsLazyCctors = context.SystemModule.GetType("System.Runtime.CompilerServices", "ClassConstructorRunner", false) != null;
            _compilationModuleGroup = compilationGroup;
            _ilprovider = ilprovider;
        }

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
