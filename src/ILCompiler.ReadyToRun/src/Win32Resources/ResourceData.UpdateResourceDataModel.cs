// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ILCompiler.Win32Resources
{
    public unsafe partial class ResourceData
    {
        private static string UpperCaseResourceString(string input)
        {
            if (input.Length > 0xFFFF)
                throw new ArgumentException();

            // Note that uppercasing only applies to lowercase, unaccented
            // Latin letters.

            char[] newString = new char[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                char upperCaseChar = input[i];
                if (upperCaseChar >= 'a' && upperCaseChar <= 'z')
                {
                    upperCaseChar = checked((char)(upperCaseChar - 'a' + 'A'));
                }
                newString[i] = upperCaseChar;
            }
            return new string(newString);
        }

        private static void ConvertToValidResourceName(ref object name)
        {
            if (name is ushort)
                return;
            else
            {
                name = UpperCaseResourceString((string)name);
            }
        }

        private void AddResource(object type, object name, ushort language, byte[] data)
        {
            ResType resType = null;
            // Allocate new object in case it is needed.
            ResType newResType;
            int newIndex;
            bool newType = false;

            IList typeList;
            bool updateExisting;
            if (type is ushort)
            {
                ResType_Ordinal newOrdinalType = new ResType_Ordinal((ushort)type);
                newResType = newOrdinalType;
                typeList = ResTypeHeadID;

                newIndex = GetIndexOfFirstItemMatchingInListOrInsertionPoint(typeList, (ushort left, ushort right) => (int)left - (int)right, (ushort)type, out updateExisting);
            }
            else
            {
                ResType_Name newStringType = new ResType_Name((string)type);
                newResType = newStringType;
                typeList = ResTypeHeadName;

                newIndex = GetIndexOfFirstItemMatchingInListOrInsertionPoint(typeList, String.CompareOrdinal, (string)type, out updateExisting);
            }

            if (updateExisting)
            {
                resType = (ResType)typeList[newIndex];
            }
            else
            {
                newType = true;

                // This is a new type
                if (newIndex == -1)
                    typeList.Add(newResType);
                else
                    typeList.Insert(newIndex, newResType);

                resType = newResType;
            }

            Type resNameType;
            IList nameList;
            int nameIndex;

            if (name is ushort)
            {
                nameList = resType.NameHeadID;
                resNameType = typeof(ResName_Ordinal);
                nameIndex = GetIndexOfFirstItemMatchingInListOrInsertionPoint(nameList, (ushort left, ushort right) => (int)left - (int)right, (ushort)name, out updateExisting);
            }
            else
            {
                nameList = resType.NameHeadName;
                resNameType = typeof(ResName_Name);
                nameIndex = GetIndexOfFirstItemMatchingInListOrInsertionPoint(nameList, String.CompareOrdinal, (string)name, out updateExisting);
            }

            if (updateExisting)
            {
                // We have at least 1 language with the same type/name. Insert/delete from language list
                ResName resName = (ResName)nameList[nameIndex];
                int newNumberOfLanguages = (int)resName.NumberOfLanguages + (data != null ? 1 : -1);

                int newIndexForNewOrUpdatedNameWithMatchingLanguage = GetIndexOfFirstItemMatchingInListOrInsertionPoint(nameList, nameIndex,
                    resName.NumberOfLanguages, (object o) => ((ResName)o).LanguageId, (ushort left, ushort right) => (int)left - (int)right, language, out bool exactLanguageExists);

                if (exactLanguageExists)
                {
                    if (data == null)
                    {
                        // delete item
                        nameList.RemoveAt(newIndexForNewOrUpdatedNameWithMatchingLanguage);

                        if (newNumberOfLanguages > 0)
                        {
                            // if another name is still present, update the number of languages counter
                            resName = (ResName)nameList[nameIndex];
                            resName.NumberOfLanguages = (ushort)newNumberOfLanguages;
                        }

                        if ((resType.NameHeadID.Count == 0) && (resType.NameHeadName.Count == 0))
                        {
                            /* type list completely empty? */
                            typeList.Remove(resType);
                        }
                    }
                    else
                    {
                        // Update item
                        throw new Exception(); // We can only reach here if the file is inconsistent.
                    }
                }
                else
                {
                    // Insert a new name at the new spot
                    AddNewName(nameList, resNameType, newIndexForNewOrUpdatedNameWithMatchingLanguage, name, language, data, newType, type);
                    // Update the NumberOfLanguages for the language list
                    resName = (ResName)nameList[nameIndex];
                    resName.NumberOfLanguages = (ushort)newNumberOfLanguages;
                }
            }
            else
            {
                // This is a new name in a new language list
                if (data == null)
                {
                    // Can't delete new name
                    throw new ArgumentException();
                }

                AddNewName(nameList, resNameType, nameIndex, name, language, data, newType, type);
            }
        }

        private static int GetIndexOfFirstItemMatchingInListOrInsertionPoint<T>(IList list, Func<T, T, int> compareFunction, T comparand, out bool exists)
        {
            return GetIndexOfFirstItemMatchingInListOrInsertionPoint(list, 0, list.Count, (object o) => ((IUnderlyingName<T>)o).Name, compareFunction, comparand, out exists);
        }

        private static int GetIndexOfFirstItemMatchingInListOrInsertionPoint<T>(IList list, int start, int count, Func<object, T> getComparandFromListElem, Func<T, T, int> compareFunction, T comparand, out bool exists)
        {
            int i = start;
            for (; i < (start + count); i++)
            {
                int iCompare = compareFunction(comparand, getComparandFromListElem(list[i]));
                if (iCompare == 0)
                {
                    exists = true;
                    return i;
                }
                else if (iCompare < 0)
                {
                    exists = false;
                    return i;
                }
            }

            exists = false;
            if ((start + count) < list.Count)
            {
                return start + count;
            }
            return -1;
        }

        void AddNewName(IList list, Type resNameType, int insertPoint, object name, ushort language, byte[] data, bool newType, object type)
        {
            ResName newResName = (ResName)Activator.CreateInstance(resNameType, name);
            newResName.LanguageId = language;
            newResName.NumberOfLanguages = 1;
            newResName.DataEntry = data;

            if (insertPoint == -1)
                list.Add(newResName);
            else
                list.Insert(insertPoint, newResName);
        }

        private byte[] FindResourceInternal(object name, object type, ushort language)
        {
            ResType resType = null;

            if (type is ushort)
            {
                foreach (var candidate in ResTypeHeadID)
                {
                    if (candidate.Type.Ordinal == (ushort)type)
                    {
                        resType = candidate;
                        break;
                    }
                }
            }
            if (type is string)
            {
                foreach (var candidate in ResTypeHeadName)
                {
                    if (candidate.Type == (string)type)
                    {
                        resType = candidate;
                        break;
                    }
                }
            }

            if (resType == null)
                return null;

            if (name is ushort)
            {
                foreach (var candidate in resType.NameHeadID)
                {
                    if (candidate.Name.Ordinal != (ushort)type)
                        continue;

                    if (candidate.LanguageId != language)
                        continue;

                    return (byte[])candidate.DataEntry.Clone();
                }
            }
            if (name is string)
            {
                foreach (var candidate in resType.NameHeadName)
                {
                    if (candidate.Name != (string)name)
                        continue;

                    if (candidate.LanguageId != language)
                        continue;

                    return (byte[])candidate.DataEntry.Clone();
                }
            }

            return null;
        }
    }
}
