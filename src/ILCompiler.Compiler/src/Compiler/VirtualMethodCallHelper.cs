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
    internal static class VirtualMethodSlotHelper
    {
        /// <summary>
        /// Given a virtual method decl, return its VTable slot if the method is used on its containing type.
        /// Return -1 if the virtual method is not used.
        /// </summary>
        public static int GetVirtualMethodSlot(NodeFactory factory, MethodDesc method)
        {
            // TODO: More efficient lookup of the slot
            TypeDesc owningType = method.OwningType;
            int baseSlots = GetNumberOfBaseSlots(factory, owningType);

            // For types that have a generic dictionary, the introduced virtual method slots are
            // prefixed with a pointer to the generic dictionary.
            if (owningType.HasGenericDictionarySlot())
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

        private static int GetNumberOfBaseSlots(NodeFactory factory, TypeDesc owningType)
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
                if (baseType.HasGenericDictionarySlot())
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
            return GetNumberOfBaseSlots(factory, type);
        }

        /// <summary>
        /// Gets a value indicating whether the virtual method slots introduced by this type are prefixed
        /// by a pointer to the generic dictionary of the type.
        /// </summary>
        public static bool HasGenericDictionarySlot(this TypeDesc type)
        {
            return !type.IsInterface &&
                (type.ConvertToCanonForm(CanonicalFormKind.Specific) != type || type.IsCanonicalSubtype(CanonicalFormKind.Any));
        }
    }
}
