// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis;

namespace ILCompiler
{
    public static class VirtualMethodSlotHelper
    {
        /// <summary>
        /// Given a virtual method decl, return its VTable slot if the method is used on its containing type.
        /// Return -1 if the virtual method is not used.
        /// </summary>
        public static int GetVirtualMethodSlot(NodeFactory factory, MethodDesc method, bool countDictionarySlots = true)
        {
            // TODO: More efficient lookup of the slot
            TypeDesc owningType = method.OwningType;
            int baseSlots = GetNumberOfBaseSlots(factory, owningType, countDictionarySlots);

            // For types that have a generic dictionary, the introduced virtual method slots are
            // prefixed with a pointer to the generic dictionary.
            if (owningType.HasGenericDictionarySlot() && countDictionarySlots)
                baseSlots++;

            IReadOnlyList<MethodDesc> virtualSlots = factory.VTable(owningType).Slots;
            int methodSlot = -1;
            for (int slot = 0; slot < virtualSlots.Count; slot++)
            {
                if (virtualSlots[slot] == method)
                {
                    methodSlot = slot;
                    break;
                }
            }

            return methodSlot == -1 ? -1 : baseSlots + methodSlot;
        }

        private static int GetNumberOfBaseSlots(NodeFactory factory, TypeDesc owningType, bool countDictionarySlots)
        {
            int baseSlots = 0;
            TypeDesc baseType = owningType.BaseType;

            while (baseType != null)
            {
                // Normalize the base type. Necessary to make this work with the lazy vtable slot
                // concept - if we start with a canonical type, the base type could end up being
                // something like Base<__Canon, string>. We would get "0 slots used" for weird
                // base types like this.
                baseType = baseType.ConvertToCanonForm(CanonicalFormKind.Specific);

                // For types that have a generic dictionary, the introduced virtual method slots are
                // prefixed with a pointer to the generic dictionary.
                if (baseType.HasGenericDictionarySlot() && countDictionarySlots)
                    baseSlots++;

                IReadOnlyList<MethodDesc> baseVirtualSlots = factory.VTable(baseType).Slots;
                baseSlots += baseVirtualSlots.Count;

                baseType = baseType.BaseType;
            }

            return baseSlots;
        }

        /// <summary>
        /// Gets the vtable slot that holds the generic dictionary of this type.
        /// </summary>
        public static int GetGenericDictionarySlot(NodeFactory factory, TypeDesc type)
        {
            Debug.Assert(type.HasGenericDictionarySlot());
            return GetNumberOfBaseSlots(factory, type, countDictionarySlots: true);
        }

        /// <summary>
        /// Gets a value indicating whether the virtual method slots introduced by this type are prefixed
        /// by a pointer to the generic dictionary of the type.
        /// </summary>
        public static bool HasGenericDictionarySlot(this TypeDesc type)
        {
            // Dictionary slots on generic interfaces are necessary to support static methods on interfaces
            // The reason behind making this unconditional is simplicity, and keeping method slot indices for methods on IFoo<int> 
            // and IFoo<string> identical. That won't change.
            if (type.IsInterface)
                return type.HasInstantiation;

            return type.HasInstantiation &&
                (type.ConvertToCanonForm(CanonicalFormKind.Specific) != type || type.IsCanonicalSubtype(CanonicalFormKind.Any));
        }
    }
}
