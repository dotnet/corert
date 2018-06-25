// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System
{
    [AttributeUsageAttribute(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public abstract partial class Attribute
    {
        protected Attribute() { }

        //
        // Compat note: .NET Core changed the behavior of Equals() relative to the full framework:
        //
        //    (https://github.com/dotnet/coreclr/pull/6240)
        //
        // This implementation implements the .NET Core behavior.
        //
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (this.GetType() != obj.GetType())
                return false;

            object[] thisFieldValues = this.ReadFields();
            object[] thatfieldValues = ((Attribute)obj).ReadFields();

            for (int i = 0; i < thisFieldValues.Length; i++)
            {
                // Visibility check and consistency check are not necessary.
                object thisResult = thisFieldValues[i];
                object thatResult = thatfieldValues[i];

                if (!AreFieldValuesEqual(thisResult, thatResult))
                {
                    return false;
                }
            }

            return true;
        }

        // Compares values of custom-attribute fields.    
        private static bool AreFieldValuesEqual(object thisValue, object thatValue)
        {
            if (thisValue == null && thatValue == null)
                return true;
            if (thisValue == null || thatValue == null)
                return false;

            Type thisValueType = thisValue.GetType();

            if (thisValueType.IsArray)
            {
                // Ensure both are arrays of the same type.
                if (!thisValueType.Equals(thatValue.GetType()))
                {
                    return false;
                }

                Array thisValueArray = thisValue as Array;
                Array thatValueArray = thatValue as Array;
                if (thisValueArray.Length != thatValueArray.Length)
                {
                    return false;
                }

                // Attributes can only contain single-dimension arrays, so we don't need to worry about 
                // multidimensional arrays.
                Debug.Assert(thisValueArray.Rank == 1 && thatValueArray.Rank == 1);
                for (int j = 0; j < thisValueArray.Length; j++)
                {
                    if (!AreFieldValuesEqual(thisValueArray.GetValue(j), thatValueArray.GetValue(j)))
                    {
                        return false;
                    }
                }
            }
            else
            {
                // An object of type Attribute will cause a stack overflow. 
                // However, this should never happen because custom attributes cannot contain values other than
                // constants, single-dimensional arrays and typeof expressions.
                Debug.Assert(!(thisValue is Attribute));
                if (!thisValue.Equals(thatValue))
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            object vThis = null;

            object[] fieldValues = this.ReadFields();
            for (int i = 0; i < fieldValues.Length; i++)
            {
                object fieldValue = fieldValues[i];

                // The hashcode of an array ignores the contents of the array, so it can produce 
                // different hashcodes for arrays with the same contents.
                // Since we do deep comparisons of arrays in Equals(), this means Equals and GetHashCode will
                // be inconsistent for arrays. Therefore, we ignore hashes of arrays.
                if (fieldValue != null && !fieldValue.GetType().IsArray)
                    vThis = fieldValue;

                if (vThis != null)
                    break;
            }

            if (vThis != null)
                return vThis.GetHashCode();

            Type type = GetType();

            return type.GetHashCode();
        }

        public virtual object TypeId => GetType();

        public virtual bool Match(object obj) => Equals(obj);

        public virtual bool IsDefaultAttribute() => false;

        //
        // This non-contract method is known to the IL transformer. See comments around _ILT_ReadFields() for more detail.
        //
        [CLSCompliant(false)]
        protected virtual int _ILT_GetNumFields()
        {
            return 0;
        }

        //
        // This non-contract method is known to the IL transformer. The IL transformer generates an override of this for each specific Attribute class.
        // Together with _ILT_GetNumFields(), it fetches the same field values that the desktop would have for comparison.
        //
        // .NET Core uses "GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)" to
        // determine the list of fields used for comparison. Unfortunately, this list can include fields that the "this" class has no right to access (e.g. "internal"
        // fields in base classes defined in another assembly.) Thus, the IL Transformer cannot simply generate a method to walk the fields and
        // be done with it. Instead, _ILT_ReadFields() directly fetches only the directly declared fields and reinvokes itself non-virtually on its
        // base class to get any inherited fields. To simplify the IL generation, the generated method only writes the results into a specified
        // offset inside a caller-supplied array. Attribute.ReadFields() calls _ILT_GetNumFields() to figure out how large an array is needed.
        //
        [CLSCompliant(false)]
        protected virtual void _ILT_ReadFields(object[] destination, int offset)
        {
        }

        //
        // ProjectN: Unlike the desktop, low level code such as Attributes cannot go running off to Reflection to fetch the FieldInfo's.
        // Instead, we use the ILTransform to generate a method that returns the relevant field values, which we then compare as the desktop does.
        //
        private object[] ReadFields()
        {
            int numFields = _ILT_GetNumFields();
            object[] fieldValues = new object[numFields];
            _ILT_ReadFields(fieldValues, 0);
            return fieldValues;
        }
    }
}
