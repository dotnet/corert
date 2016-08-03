﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis;

namespace ILCompiler
{
    static class VirtualMethodSlotHelper
    {
        /// <summary>
        /// Given a virtual method decl, return its VTable slot if the method is used on its containing type.
        /// Return -1 if the virtual method is not used.
        /// </summary>
        public static int GetVirtualMethodSlot(NodeFactory factory, MethodDesc method)
        {
            // TODO: More efficient lookup of the slot
            TypeDesc owningType = method.OwningType;
            int baseSlots = 0;
            var baseType = owningType.BaseType;

            while (baseType != null)
            {
                IReadOnlyList<MethodDesc> baseVirtualSlots = factory.VTable(baseType).Slots;
                baseSlots += baseVirtualSlots.Count;

                if (baseType.HasGenericDictionarySlot())
                    baseSlots++;

                baseType = baseType.BaseType;
            }

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

        public static int GetGenericDictionarySlot(NodeFactory factory, TypeDesc type)
        {
            int baseSlots = 0;
            var baseType = type;

            while (baseType != null)
            {
                IReadOnlyList<MethodDesc> baseVirtualSlots = factory.VTable(baseType).Slots;
                baseSlots += baseVirtualSlots.Count;

                if (baseType.HasGenericDictionarySlot())
                    baseSlots++;

                baseType = baseType.BaseType;
            }

            return baseSlots;
        }

        public static bool HasGenericDictionarySlot(this TypeDesc type)
        {
            return type.ConvertToCanonForm(CanonicalFormKind.Specific) != type;
        }
    }
}
