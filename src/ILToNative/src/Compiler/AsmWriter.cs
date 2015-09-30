// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

namespace ILToNative
{
    public partial class Compilation
    {
        void OutputCode()
        {
            Out.WriteLine(".text");

            foreach (var t in _registeredTypes.Values)
            {
                RegisteredMethod m = t.Methods;
                while (m != null)
                {
                    if (m.MethodCode != null)
                        OutputMethodCode(m);
                    m = m.Next;
                }
            }

            Out.WriteLine("//" + new string('-', 80));
            Out.WriteLine();

            OutputReadyToHelpers();

            OutputEETypes();

            Out.Dispose();
        }

        void OutputMethodCode(RegisteredMethod m)
        {
            string mangledName = GetMangledMethodName(m.Method);

            Out.Write(".global ");
            Out.WriteLine(mangledName);

            Out.Write(mangledName);
            Out.WriteLine(":");

            var methodCode = m.MethodCode;

            Relocation[] relocs = methodCode.Relocs;
            int currentRelocIndex = 0;
            int nextRelocOffset = -1;

            byte[] code = methodCode.Code;

            if (relocs != null)
            {
                nextRelocOffset = relocs[currentRelocIndex].Offset;
                if (relocs[currentRelocIndex].RelocType == 0x10) // IMAGE_REL_BASED_REL32
                    nextRelocOffset--;
            }

            int lineLength = 0;
            for (int i = 0; i < code.Length; i++)
            {
                if (i == nextRelocOffset)
                {
                    if (lineLength > 0)
                    {
                        Out.WriteLine();
                        lineLength = 0;
                    }

                    Relocation reloc = relocs[currentRelocIndex];

                    Object target = reloc.Target;
                    string targetName;
                    if (target is MethodDesc)
                    {
                        targetName = GetMangledMethodName((MethodDesc)target);
                    }
                    else
                    if (target is ReadyToRunHelper)
                    {
                        targetName = ((ReadyToRunHelper)target).MangledName;
                    }
                    else
                    {
                        // TODO:
                        throw new NotImplementedException();
                    }

                    switch (reloc.RelocType)
                    {
                        case 0x0A: // IMAGE_REL_BASED_DIR64
                            Out.Write(".quad ");
                            Out.WriteLine(targetName);
                            i += 7;
                            break;
                        case 0x10:
                            if (code[i] != 0xE8) // call
                                throw new NotImplementedException();
                            Out.Write("call ");
                            Out.WriteLine(targetName);
                            i += 4;
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    currentRelocIndex++;
                    nextRelocOffset = -1;
                    if (currentRelocIndex < relocs.Length)
                    {
                        nextRelocOffset = relocs[currentRelocIndex].Offset;
                        if (relocs[currentRelocIndex].RelocType == 0x10) // IMAGE_REL_BASED_REL32
                            nextRelocOffset--;
                    }

                    continue;
                }

                if (lineLength == 0)
                {
                    Out.Write(".byte ");
                }
                else
                {
                    Out.Write(",");
                }

                Out.Write(code[i]);

                if (lineLength++ > 15)
                {
                    Out.WriteLine();
                    lineLength = 0;
                }
            }

            // TODO: ColdCode
            if (methodCode.ColdCode != null)
                throw new NotImplementedException();

            // TODO: ROData
            if (methodCode.ROData != null)
                throw new NotImplementedException();

            if (lineLength > 0)
                Out.WriteLine();
            Out.WriteLine();
        }

        void OutputReadyToHelpers()
        {
            foreach (var helper in _readyToRunHelpers.Values)
            {
                Out.Write(helper.MangledName);
                Out.WriteLine(":");

                switch (helper.Id)
                {
                    case ReadyToRunHelperId.NewHelper:
                        Out.Write("leaq __EEType_");
                        Out.Write(GetMangledTypeName((TypeDesc)helper.Target));
                        Out.WriteLine("(%rip), %rcx");

                        Out.WriteLine("jmp __allocate_object");
                        break;

                    case ReadyToRunHelperId.VirtualCall:
                        Out.WriteLine("movq (%rcx), %rax");
                        Out.Write("jmp *");

                        // TODO: More efficient lookup of the slot
                        {
                            MethodDesc method = (MethodDesc)helper.Target;
                            TypeDesc owningType = method.OwningType;

                            int baseSlots = 0;
                            var baseType = owningType.BaseType;

                            while (baseType != null)
                            {
                                var baseReg = GetRegisteredType(baseType);
                                if (baseReg.VirtualSlots != null)
                                    baseSlots += baseReg.VirtualSlots.Count;
                                baseType = baseType.BaseType;
                            }

                            var t = GetRegisteredType(owningType);
                            int methodSlot = -1;
                            for (int slot = 0; slot < t.VirtualSlots.Count; slot++)
                            {
                                if (t.VirtualSlots[slot] == method)
                                {
                                    methodSlot = slot;
                                    break;
                                }
                            }

                            Debug.Assert(methodSlot != -1);
                            Out.Write(16 /* sizeof(EEType */ + (baseSlots + methodSlot) * _typeSystemContext.Target.PointerSize);
                        }

                        Out.WriteLine("(%rax)");
                        break;

                    case ReadyToRunHelperId.IsInstanceOf:
                        Out.Write("leaq __EEType_");
                        Out.Write(GetMangledTypeName((TypeDesc)helper.Target));
                        Out.WriteLine("(%rip), %rdx");

                        Out.WriteLine("jmp __isinst_class");
                        break;

                    case ReadyToRunHelperId.CastClass:
                        Out.Write("leaq __EEType_");
                        Out.Write(GetMangledTypeName((TypeDesc)helper.Target));
                        Out.WriteLine("(%rip), %rdx");

                        Out.WriteLine("jmp __castclass_class");
                        break;

                    default:
                        throw new NotImplementedException();
                }
                Out.WriteLine();
            }
        }

        void OutputEETypes()
        {
            foreach (var t in _registeredTypes.Values)
            {
                if (!t.IncludedInCompilation)
                    continue;

                Out.WriteLine(".align 16");
                Out.Write("__EEType_");
                Out.Write(GetMangledTypeName(t.Type));
                Out.WriteLine(":");

                Out.WriteLine(".int 0, 24");

                if (t.Type.BaseType != null)
                {
                    Out.Write(".quad __EEType_");
                    Out.WriteLine(GetMangledTypeName(t.Type.BaseType));
                }
                else
                {
                    Out.WriteLine(".quad 0");
                }

                if (t.Constructed)
                    OutputVirtualSlots(t.Type, t.Type);

                Out.WriteLine();
            }
        }

        void OutputVirtualSlots(TypeDesc implType, TypeDesc declType)
        {
            var baseType = declType.BaseType;
            if (baseType != null)
                OutputVirtualSlots(implType, baseType);

            var reg = GetRegisteredType(declType);
            if (reg.VirtualSlots != null)
            {
                for (int i = 0; i < reg.VirtualSlots.Count; i++)
                {
                    MethodDesc declMethod = reg.VirtualSlots[i];

                    MethodDesc implMethod = ResolveVirtualMethod(implType, declMethod);

                    Out.Write(".quad ");
                    Out.WriteLine(GetMangledMethodName(implMethod));
                }
            }
        }
    }
}