// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Contains utility functionality for canonicalization used by multiple types
    /// </summary>
    class CanonUtilites
    {
        /// <summary>
        /// Returns true if canonicalizing this instantiation produces a different instantiation
        /// </summary>
        /// <param name="kind">Canonicalization policy to apply</param>
        public static bool ConversionToCanonFormIsAChange(TypeSystemContext context, Instantiation instantiation, CanonicalFormKind kind)
        {
            foreach (TypeDesc type in instantiation)
            {
                TypeDesc potentialCanonType = ConvertToCanon(type, kind);
                if (type != potentialCanonType)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a new instantiation that canonicalizes all types in <paramref name="instantiation"/>
        /// if possible under the policy of <paramref name="kind"/>
        /// </summary>
        public static Instantiation ConvertInstantiationToCanonForm(TypeSystemContext context, Instantiation instantiation, CanonicalFormKind kind)
        {
            TypeDesc[] newInstantiation = new TypeDesc[instantiation.Length];

            for (int i = 0; i < instantiation.Length; i++)
            {
                newInstantiation[i] = ConvertToCanon(instantiation[i], kind);
            }

            return new Instantiation(newInstantiation);
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
                if (typeToConvert is DefType)
                {
                    if (typeToConvert == context.UniversalCanonType)
                        return context.UniversalCanonType;

                    DefType defTypeToConvert = (DefType)typeToConvert;

                    if (!defTypeToConvert.IsValueType)
                        return context.CanonType;
                    else if (defTypeToConvert.HasInstantiation)
                        return defTypeToConvert.ConvertToCanonForm(CanonicalFormKind.Specific);
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
