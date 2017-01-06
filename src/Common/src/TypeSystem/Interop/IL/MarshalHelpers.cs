// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;
using Internal.IL;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem.Interop
{
    public static class MarshalHelpers
    {
        /// <summary>
        /// Returns true if this is a type that doesn't require marshalling.
        /// </summary>
        public static bool IsBlittableType(TypeDesc type)
        {
            type = type.UnderlyingType;

            if (type.IsValueType)
            {
                if (type.IsPrimitive)
                {
                    // All primitive types except char and bool are blittable
                    TypeFlags category = type.Category;
                    if (category == TypeFlags.Boolean || category == TypeFlags.Char)
                        return false;

                    return true;
                }

                foreach (FieldDesc field in type.GetFields())
                {
                    if (field.IsStatic)
                        continue;

                    TypeDesc fieldType = field.FieldType;

                    // TODO: we should also reject fields that specify custom marshalling
                    if (!MarshalHelpers.IsBlittableType(fieldType))
                    {
                        // This field can still be blittable if it's a Char and marshals as Unicode
                        var owningType = field.OwningType as MetadataType;
                        if (owningType == null)
                            return false;

                        if (fieldType.Category != TypeFlags.Char ||
                            owningType.PInvokeStringFormat == PInvokeStringFormat.AnsiClass)
                            return false;
                    }
                }
                return true;
            }

            if (type.IsPointer || type.IsFunctionPointer)
                return true;

            return false;
        }

        /// <summary>
        /// Returns true if <paramref name="method"/> requires a stub to be generated.
        /// </summary>
        public static bool IsStubRequired(MethodDesc method, PInvokeILEmitterConfiguration configuration)
        {
            Debug.Assert(method.IsPInvoke);

            // TODO: true if there are any custom marshalling rules on the parameters

            TypeDesc returnType = method.Signature.ReturnType;
            if (!MarshalHelpers.IsBlittableType(returnType) && !returnType.IsVoid)
                return true;

            for (int i = 0; i < method.Signature.Length; i++)
            {
                if (!MarshalHelpers.IsBlittableType(method.Signature[i]))
                {
                    return true;
                }
            }

            PInvokeMetadata methodData = method.GetPInvokeMethodMetadata();    
            if (UseLazyResolution(method, methodData.Module, configuration))
            {
                return true;
            }
            if ((methodData.Attributes & PInvokeAttributes.SetLastError) == PInvokeAttributes.SetLastError)
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// Returns true if the PInvoke target should be resolved lazily.
        /// </summary>
        public static bool UseLazyResolution(MethodDesc method, string importModule, PInvokeILEmitterConfiguration configuration)
        {
            // TODO: Test and make this work on non-Windows
            if (!method.Context.Target.IsWindows)
                return false;

            if (configuration.ForceLazyResolution)
                return true;

            // Determine whether this call should be made through a lazy resolution or a static reference
            // Eventually, this should be controlled by a custom attribute (or an extension to the metadata format).
            if (importModule == "[MRT]" || importModule == "*")
                return false;

            if (method.Context.Target.IsWindows)
                return !importModule.StartsWith("api-ms-win-");
            else
                return !importModule.StartsWith("System.Private.");
        }
    }
}