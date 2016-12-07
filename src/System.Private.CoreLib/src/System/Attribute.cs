// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System
{
    [AttributeUsageAttribute(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract partial class Attribute
    {
        protected Attribute() { }

        public override bool Equals(Object obj)
        {
            if (obj == null)
                return false;

            if (this.GetType() != obj.GetType())
                return false;

            Object[] thisFieldValues = this.ReadFields();
            Object[] thatfieldValues = ((Attribute)obj).ReadFields();

            for (int i = 0; i < thisFieldValues.Length; i++)
            {
                // Visibility check and consistency check are not necessary.
                Object thisResult = thisFieldValues[i];
                Object thatResult = thatfieldValues[i];

                if (!AreFieldValuesEqual(thisResult, thatResult))
                {
                    return false;
                }
            }

            return true;
        }

        // Compares values of custom-attribute fields.    
        private static bool AreFieldValuesEqual(Object thisValue, Object thatValue)
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
            Object vThis = null;

            Object[] fieldValues = this.ReadFields();
            for (int i = 0; i < fieldValues.Length; i++)
            {
                Object fieldValue = fieldValues[i];

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


        //
        // This non-contract method is known to the IL transformer. See comments around _ILT_ReadFields() for more detail.
        //
        [CLSCompliant(false)]
        protected virtual int _ILT_GetNumFields(bool inBaseClass)
        {
            return 0;
        }

        //
        // This non-contract method is known to the IL transformer. The IL transformer generates an override of this for each specific Attribute class.
        // Together with _ILT_GetNumFields(), it fetches the same field values that the desktop would have for comparison.
        //
        // The desktop uses "GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)" to determine the list of fields
        // used for comparison. Unfortunately, this list can include fields that the "this" class has no right to access (e.g. "internal"
        // fields in base classes defined in another assembly.) Thus, the IL Transformer cannot simply generate a method to walk the fields and
        // be done with it. Instead, _ILT_ReadFields() directly fetches only the directly declared fields and reinvokes itself non-virtually on its
        // base class to get any inherited fields. To simplify the IL generation, the generated method only writes the results into a specified
        // offset inside a caller-supplied array. Attribute.ReadFields() calls _ILT_GetNumFields() to figure out how large an array is needed.
        //
        // The "inBaseClass" determines whether the "this" is the "this" that Equals/GetHashCode was actually called or one of its base types.
        // That's because the list of fields includes directly declared private fields but not inherited private fields. (This can in turn
        // cause weird effects like the derived type returning "true" for Equals() while it's base type's Equal() returns "false" - but that's
        // backward compat for you.)
        //
        [CLSCompliant(false)]
        protected virtual void _ILT_ReadFields(Object[] destination, int offset, bool inBaseClass)
        {
        }

        //
        // ProjectN: Unlike the desktop, low level code such as Attributes cannot go running off to Reflection to fetch the FieldInfo's.
        // Instead, we use the ILTransform to generate a method that returns the relevant field values, which we then compare as the desktop does.
        //
        private Object[] ReadFields()
        {
            int numFields = _ILT_GetNumFields(inBaseClass: false);
            Object[] fieldValues = new Object[numFields];
            _ILT_ReadFields(fieldValues, 0, inBaseClass: false);
            return fieldValues;
        }
    }
}
