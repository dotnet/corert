// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Structure used to wrap a GenericInstanceDesc if we're not in standalone mode (a single exe with no further
// Redhawk dependencies).
//
// In such cases we might have several modules with local definitions of the same generic instantiation and in
// order to make these disjoint EETypes type compatible we have to unify them somehow. This is achieved by
// comparing all the generic instantiations a module contributes when it loads with the existing generic
// instantiations, using the data in the GenericInstanceDesc to determine type identity. When a new
// instantiation is found we allocate a new EEType and GenericInstanceDesc to represent the canonical version
// of the type (we allocate new versions rather that utilizing the version baked into the introducing module
// so as to support the module unload scenario). When a module contributes a duplicate generic instantiation
// it finds these existing definitions and is unified to use them for certain operations that require the
// unique instantiation property (e.g. casting or access to static field data). The mechanism for the unifying
// redirect for EETypes is cloning (all module local generic EETypes become clones of the runtime allocated
// canonical EEType). For GenericInstanceDesc we use the following structure to track the canonical,
// runtime-allocated GenericInstanceDesc and also update fields in each module local GenericInstanceDesc that
// serve as indirection cells for static field lookup to match the values in the canonical version.
//
// A UnifiedGenericInstance structure is always immediately followed by a variable sized GenericInstanceDesc
// (the canonical copy).
//
// In the standalone case we never unify generic types; the single module continues to use the local
// non-cloned EEType and GenericInstanceDesc with their binder created values and we never allocate any
// UnifiedGenericInstance structures or EEType or GenericInstanceDesc copies.
//
// We determine which mode we're in (standlone or not) via a flag in the module header.
struct UnifiedGenericInstance
{
    UnifiedGenericInstance *    m_pNext;            // Next entry in the hash table chain
    UInt32                      m_cRefs;            // Number of modules which have published this type

    bool Equals(GenericInstanceDesc * pInst);
    GenericInstanceDesc * GetGid() { return (GenericInstanceDesc*)(this + 1); }
};
