// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Contains utility functionality for canonicalization used by multiple types.
    /// </summary>
    public static class StandardCanonicalizationAlgorithm
    {
        /// <summary>
        /// Returns a new instantiation that canonicalizes all types in <paramref name="instantiation"/>
        /// if possible under the policy of '<paramref name="kind"/>'
        /// </summary>
        /// <param name="changed">True if the returned instantiation is different from '<paramref name="instantiation"/>'.</param>
        public static Instantiation ConvertInstantiationToCanonForm(Instantiation instantiation, CanonicalFormKind kind, out bool changed)
        {
            TypeDesc[] newInstantiation = null;

            for (int i = 0; i < instantiation.Length; i++)
            {
                TypeDesc typeToConvert = instantiation[i];
                TypeDesc convertedType = ConvertToCanon(typeToConvert, kind);

                if (typeToConvert != convertedType || newInstantiation != null)
                {
                    if (newInstantiation == null)
                    {
                        newInstantiation = new TypeDesc[instantiation.Length];
                        for (int j = 0; j < i; j++)
                            newInstantiation[j] = instantiation[j];
                    }

                    newInstantiation[i] = convertedType;
                }
            }

            changed = newInstantiation != null;
            if (changed)
            {
                return new Instantiation(newInstantiation);
            }

            return instantiation;
        }

        /// <summary>
        /// Helper API to convert a type to its canonical or universal canonical form.
        /// Note that for now, there is no mixture between specific canonical and universal canonical forms,
        /// meaning that the canonical form or Foo<string, int> can either be Foo<__Canon, int> or
        /// Foo<__UniversalCanon, __UniversalCanon>. It cannot be Foo<__Canon, __UniversalCanon> (yet)
        /// for simplicity. We can always change that rule in the futue and add support for the mixture, but
        /// for now we are keeping it simple.
        /// </summary>
        public static TypeDesc ConvertToCanon(TypeDesc typeToConvert, CanonicalFormKind kind)
        {
            TypeSystemContext context = typeToConvert.Context;
            if (kind == CanonicalFormKind.Universal)
            {
                return context.UniversalCanonType;
            }
            else if (kind == CanonicalFormKind.Specific)
            {
                if (typeToConvert == context.UniversalCanonType)
                {
                    return context.UniversalCanonType;
                }
                else if (typeToConvert.IsSignatureVariable)
                {
                    return typeToConvert;
                }
                else if (typeToConvert.IsDefType)
                {
                    if (!typeToConvert.IsValueType)
                        return context.CanonType;
                    else if (typeToConvert.HasInstantiation)
                        return typeToConvert.ConvertToCanonForm(CanonicalFormKind.Specific);
                    else
                        return typeToConvert;
                }
                else if (typeToConvert.IsArray)
                {
                    return context.CanonType;
                }
                else
                {
                    return typeToConvert.ConvertToCanonForm(CanonicalFormKind.Specific);
                }
            }
            else
            {
                Debug.Assert(false);
                return null;
            }
        }
    }
}
