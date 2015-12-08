using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis;

namespace ILCompiler
{
    class VirtualMethodSlotHelper
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
                List<MethodDesc> baseVirtualSlots;
                factory.VirtualSlots.TryGetValue(baseType, out baseVirtualSlots);

                if (baseVirtualSlots != null)
                    baseSlots += baseVirtualSlots.Count;
                baseType = baseType.BaseType;
            }

            List<MethodDesc> virtualSlots = factory.VirtualSlots[owningType];
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
    }
}
