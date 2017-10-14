// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Collections.Generic
{
    #region ArraySortHelper for single arrays

    internal static class IntrospectiveSortUtilities
    {
        // This is the threshold where Introspective sort switches to Insertion sort.
        // Imperically, 16 seems to speed up most cases without slowing down others, at least for integers.
        // Large value types may benefit from a smaller number.
        internal const int IntrosortSizeThreshold = 16;

        internal static int FloorLog2(int n)
        {
            int result = 0;
            while (n >= 1)
            {
                result++;
                n = n / 2;
            }
            return result;
        }

        internal static void ThrowOrIgnoreBadComparer(Object comparer)
        {
            // This is hit when an invarant of QuickSort is violated due to a bad IComparer implementation (for
            // example, imagine an IComparer that returns 0 when items are equal but -1 all other times).
            throw new ArgumentException(SR.Format(SR.Arg_BogusIComparer, comparer));
        }
    }

    internal class ArraySortHelper<T>
    {
        #region ArraySortHelper<T> public Members

        public static void Sort(T[] keys, int index, int length, IComparer<T> comparer)
        {
            Debug.Assert(keys != null, "Check the arguments in the caller!");
            Debug.Assert(index >= 0 && length >= 0 && (keys.Length - index >= length), "Check the arguments in the caller!");

            // Add a try block here to detect IComparers (or their
            // underlying IComparables, etc) that are bogus.
            try
            {
                if (comparer == null)
                {
                    comparer = Comparer<T>.Default;
                }

                IntrospectiveSort(keys, index, length, comparer.Compare);
            }
            catch (IndexOutOfRangeException)
            {
                IntrospectiveSortUtilities.ThrowOrIgnoreBadComparer(comparer);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(SR.InvalidOperation_IComparerFailed, e);
            }
        }

        public static int BinarySearch(T[] array, int index, int length, T value, IComparer<T> comparer)
        {
            try
            {
                if (comparer == null)
                {
                    comparer = Comparer<T>.Default;
                }

                return InternalBinarySearch(array, index, length, value, comparer);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(SR.InvalidOperation_IComparerFailed, e);
            }
        }

        #endregion

        internal static void Sort(T[] keys, int index, int length, Comparison<T> comparer)
        {
            Debug.Assert(keys != null, "Check the arguments in the caller!");
            Debug.Assert(index >= 0 && length >= 0 && (keys.Length - index >= length), "Check the arguments in the caller!");
            Debug.Assert(comparer != null, "Check the arguments in the caller!");

            // Add a try block here to detect bogus comparisons
            try
            {
                IntrospectiveSort(keys, index, length, comparer);
            }
            catch (IndexOutOfRangeException)
            {
                IntrospectiveSortUtilities.ThrowOrIgnoreBadComparer(comparer);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(SR.InvalidOperation_IComparerFailed, e);
            }
        }

        internal static int InternalBinarySearch(T[] array, int index, int length, T value, IComparer<T> comparer)
        {
            Debug.Assert(array != null, "Check the arguments in the caller!");
            Debug.Assert(index >= 0 && length >= 0 && (array.Length - index >= length), "Check the arguments in the caller!");

            int lo = index;
            int hi = index + length - 1;
            while (lo <= hi)
            {
                int i = lo + ((hi - lo) >> 1);
                int order = comparer.Compare(array[i], value);

                if (order == 0) return i;
                if (order < 0)
                {
                    lo = i + 1;
                }
                else
                {
                    hi = i - 1;
                }
            }

            return ~lo;
        }

        private static void SwapIfGreater(T[] keys, Comparison<T> comparer, int a, int b)
        {
            if (a != b)
            {
                if (comparer(keys[a], keys[b]) > 0)
                {
                    T key = keys[a];
                    keys[a] = keys[b];
                    keys[b] = key;
                }
            }
        }

        private static void Swap(T[] a, int i, int j)
        {
            if (i != j)
            {
                T t = a[i];
                a[i] = a[j];
                a[j] = t;
            }
        }

        internal static void IntrospectiveSort(T[] keys, int left, int length, Comparison<T> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(comparer != null);
            Debug.Assert(left >= 0);
            Debug.Assert(length >= 0);
            Debug.Assert(length <= keys.Length);
            Debug.Assert(length + left <= keys.Length);

            if (length < 2)
                return;

            IntroSort(keys, left, length + left - 1, 2 * IntrospectiveSortUtilities.FloorLog2(keys.Length), comparer);
        }

        private static void IntroSort(T[] keys, int lo, int hi, int depthLimit, Comparison<T> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(comparer != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(hi < keys.Length);

            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= IntrospectiveSortUtilities.IntrosortSizeThreshold)
                {
                    if (partitionSize == 1)
                    {
                        return;
                    }
                    if (partitionSize == 2)
                    {
                        SwapIfGreater(keys, comparer, lo, hi);
                        return;
                    }
                    if (partitionSize == 3)
                    {
                        SwapIfGreater(keys, comparer, lo, hi - 1);
                        SwapIfGreater(keys, comparer, lo, hi);
                        SwapIfGreater(keys, comparer, hi - 1, hi);
                        return;
                    }

                    InsertionSort(keys, lo, hi, comparer);
                    return;
                }

                if (depthLimit == 0)
                {
                    Heapsort(keys, lo, hi, comparer);
                    return;
                }
                depthLimit--;

                int p = PickPivotAndPartition(keys, lo, hi, comparer);
                // Note we've already partitioned around the pivot and do not have to move the pivot again.
                IntroSort(keys, p + 1, hi, depthLimit, comparer);
                hi = p - 1;
            }
        }

        private static int PickPivotAndPartition(T[] keys, int lo, int hi, Comparison<T> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(comparer != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(hi > lo);
            Debug.Assert(hi < keys.Length);

            // Compute median-of-three.  But also partition them, since we've done the comparison.
            int middle = lo + ((hi - lo) / 2);

            // Sort lo, mid and hi appropriately, then pick mid as the pivot.
            SwapIfGreater(keys, comparer, lo, middle);  // swap the low with the mid point
            SwapIfGreater(keys, comparer, lo, hi);   // swap the low with the high
            SwapIfGreater(keys, comparer, middle, hi); // swap the middle with the high

            T pivot = keys[middle];
            Swap(keys, middle, hi - 1);
            int left = lo, right = hi - 1;  // We already partitioned lo and hi and put the pivot in hi - 1.  And we pre-increment & decrement below.

            while (left < right)
            {
                while (comparer(keys[++left], pivot) < 0) ;
                while (comparer(pivot, keys[--right]) < 0) ;

                if (left >= right)
                    break;

                Swap(keys, left, right);
            }

            // Put pivot in the right location.
            Swap(keys, left, (hi - 1));
            return left;
        }

        private static void Heapsort(T[] keys, int lo, int hi, Comparison<T> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(comparer != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(hi > lo);
            Debug.Assert(hi < keys.Length);

            int n = hi - lo + 1;
            for (int i = n / 2; i >= 1; i = i - 1)
            {
                DownHeap(keys, i, n, lo, comparer);
            }
            for (int i = n; i > 1; i = i - 1)
            {
                Swap(keys, lo, lo + i - 1);
                DownHeap(keys, 1, i - 1, lo, comparer);
            }
        }

        private static void DownHeap(T[] keys, int i, int n, int lo, Comparison<T> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(comparer != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(lo < keys.Length);

            T d = keys[lo + i - 1];
            int child;
            while (i <= n / 2)
            {
                child = 2 * i;
                if (child < n && comparer(keys[lo + child - 1], keys[lo + child]) < 0)
                {
                    child++;
                }
                if (!(comparer(d, keys[lo + child - 1]) < 0))
                    break;
                keys[lo + i - 1] = keys[lo + child - 1];
                i = child;
            }
            keys[lo + i - 1] = d;
        }

        private static void InsertionSort(T[] keys, int lo, int hi, Comparison<T> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(hi >= lo);
            Debug.Assert(hi <= keys.Length);

            int i, j;
            T t;
            for (i = lo; i < hi; i++)
            {
                j = i;
                t = keys[i + 1];
                while (j >= lo && comparer(t, keys[j]) < 0)
                {
                    keys[j + 1] = keys[j];
                    j--;
                }
                keys[j + 1] = t;
            }
        }
    }


    #endregion

    #region ArraySortHelper for paired key and value arrays

    internal class ArraySortHelper<TKey, TValue>
    {
        // WARNING: We allow diagnostic tools to directly inspect this member (s_defaultArraySortHelper). 
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        private static volatile ArraySortHelper<TKey, TValue> s_defaultArraySortHelper;

        public static ArraySortHelper<TKey, TValue> Default
        {
            get
            {
                ArraySortHelper<TKey, TValue> sorter = s_defaultArraySortHelper;
                if (sorter == null)
                    sorter = CreateArraySortHelper();

                return sorter;
            }
        }

        private static ArraySortHelper<TKey, TValue> CreateArraySortHelper()
        {
            s_defaultArraySortHelper = new ArraySortHelper<TKey, TValue>();
            return s_defaultArraySortHelper;
        }

        public void Sort(TKey[] keys, TValue[] values, int index, int length, IComparer<TKey> comparer)
        {
            Debug.Assert(keys != null, "Check the arguments in the caller!");  // Precondition on interface method
            Debug.Assert(index >= 0 && length >= 0 && (keys.Length - index >= length), "Check the arguments in the caller!");

            // Add a try block here to detect IComparers (or their
            // underlying IComparables, etc) that are bogus.
            try
            {
                if (comparer == null || comparer == Comparer<TKey>.Default)
                {
                    comparer = Comparer<TKey>.Default;
                }

                IntrospectiveSort(keys, values, index, length, comparer);
            }
            catch (IndexOutOfRangeException)
            {
                IntrospectiveSortUtilities.ThrowOrIgnoreBadComparer(comparer);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(SR.InvalidOperation_IComparerFailed, e);
            }
        }

        private static void SwapIfGreaterWithItems(TKey[] keys, TValue[] values, IComparer<TKey> comparer, int a, int b)
        {
            Debug.Assert(keys != null);
            Debug.Assert(comparer != null);
            Debug.Assert(0 <= a && a < keys.Length);
            Debug.Assert(0 <= b && b < keys.Length);
            Debug.Assert(values == null || (0 <= a && a < values.Length));
            Debug.Assert(values == null || (0 <= b && b < values.Length));

            if (a != b)
            {
                if (comparer.Compare(keys[a], keys[b]) > 0)
                {
                    TKey key = keys[a];
                    keys[a] = keys[b];
                    keys[b] = key;
                    if (values != null)
                    {
                        TValue value = values[a];
                        values[a] = values[b];
                        values[b] = value;
                    }
                }
            }
        }

        private static void Swap(TKey[] keys, TValue[] values, int i, int j)
        {
            if (i != j)
            {
                TKey k = keys[i];
                keys[i] = keys[j];
                keys[j] = k;
                if (values != null)
                {
                    TValue v = values[i];
                    values[i] = values[j];
                    values[j] = v;
                }
            }
        }

        internal static void IntrospectiveSort(TKey[] keys, TValue[] values, int left, int length, IComparer<TKey> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(values != null);
            Debug.Assert(comparer != null);
            Debug.Assert(left >= 0);
            Debug.Assert(length >= 0);
            Debug.Assert(length <= keys.Length);
            Debug.Assert(length + left <= keys.Length);
            Debug.Assert(length + left <= values.Length);

            if (length < 2)
                return;

            IntroSort(keys, values, left, length + left - 1, 2 * IntrospectiveSortUtilities.FloorLog2(keys.Length), comparer);
        }

        private static void IntroSort(TKey[] keys, TValue[] values, int lo, int hi, int depthLimit, IComparer<TKey> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(values != null);
            Debug.Assert(comparer != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(hi < keys.Length);

            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= IntrospectiveSortUtilities.IntrosortSizeThreshold)
                {
                    if (partitionSize == 1)
                    {
                        return;
                    }
                    if (partitionSize == 2)
                    {
                        SwapIfGreaterWithItems(keys, values, comparer, lo, hi);
                        return;
                    }
                    if (partitionSize == 3)
                    {
                        SwapIfGreaterWithItems(keys, values, comparer, lo, hi - 1);
                        SwapIfGreaterWithItems(keys, values, comparer, lo, hi);
                        SwapIfGreaterWithItems(keys, values, comparer, hi - 1, hi);
                        return;
                    }

                    InsertionSort(keys, values, lo, hi, comparer);
                    return;
                }

                if (depthLimit == 0)
                {
                    Heapsort(keys, values, lo, hi, comparer);
                    return;
                }
                depthLimit--;

                int p = PickPivotAndPartition(keys, values, lo, hi, comparer);
                // Note we've already partitioned around the pivot and do not have to move the pivot again.
                IntroSort(keys, values, p + 1, hi, depthLimit, comparer);
                hi = p - 1;
            }
        }

        private static int PickPivotAndPartition(TKey[] keys, TValue[] values, int lo, int hi, IComparer<TKey> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(values != null);
            Debug.Assert(comparer != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(hi > lo);
            Debug.Assert(hi < keys.Length);

            // Compute median-of-three.  But also partition them, since we've done the comparison.
            int middle = lo + ((hi - lo) / 2);

            // Sort lo, mid and hi appropriately, then pick mid as the pivot.
            SwapIfGreaterWithItems(keys, values, comparer, lo, middle);  // swap the low with the mid point
            SwapIfGreaterWithItems(keys, values, comparer, lo, hi);   // swap the low with the high
            SwapIfGreaterWithItems(keys, values, comparer, middle, hi); // swap the middle with the high

            TKey pivot = keys[middle];
            Swap(keys, values, middle, hi - 1);
            int left = lo, right = hi - 1;  // We already partitioned lo and hi and put the pivot in hi - 1.  And we pre-increment & decrement below.

            while (left < right)
            {
                while (comparer.Compare(keys[++left], pivot) < 0) ;
                while (comparer.Compare(pivot, keys[--right]) < 0) ;

                if (left >= right)
                    break;

                Swap(keys, values, left, right);
            }

            // Put pivot in the right location.
            Swap(keys, values, left, (hi - 1));
            return left;
        }

        private static void Heapsort(TKey[] keys, TValue[] values, int lo, int hi, IComparer<TKey> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(values != null);
            Debug.Assert(comparer != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(hi > lo);
            Debug.Assert(hi < keys.Length);

            int n = hi - lo + 1;
            for (int i = n / 2; i >= 1; i = i - 1)
            {
                DownHeap(keys, values, i, n, lo, comparer);
            }
            for (int i = n; i > 1; i = i - 1)
            {
                Swap(keys, values, lo, lo + i - 1);
                DownHeap(keys, values, 1, i - 1, lo, comparer);
            }
        }

        private static void DownHeap(TKey[] keys, TValue[] values, int i, int n, int lo, IComparer<TKey> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(comparer != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(lo < keys.Length);

            TKey d = keys[lo + i - 1];
            TValue dValue = (values != null) ? values[lo + i - 1] : default(TValue);
            int child;
            while (i <= n / 2)
            {
                child = 2 * i;
                if (child < n && comparer.Compare(keys[lo + child - 1], keys[lo + child]) < 0)
                {
                    child++;
                }
                if (!(comparer.Compare(d, keys[lo + child - 1]) < 0))
                    break;
                keys[lo + i - 1] = keys[lo + child - 1];
                if (values != null)
                    values[lo + i - 1] = values[lo + child - 1];
                i = child;
            }
            keys[lo + i - 1] = d;
            if (values != null)
                values[lo + i - 1] = dValue;
        }

        private static void InsertionSort(TKey[] keys, TValue[] values, int lo, int hi, IComparer<TKey> comparer)
        {
            Debug.Assert(keys != null);
            Debug.Assert(values != null);
            Debug.Assert(comparer != null);
            Debug.Assert(lo >= 0);
            Debug.Assert(hi >= lo);
            Debug.Assert(hi <= keys.Length);

            int i, j;
            TKey t;
            TValue tValue;
            for (i = lo; i < hi; i++)
            {
                j = i;
                t = keys[i + 1];
                tValue = (values != null) ? values[i + 1] : default(TValue);
                while (j >= lo && comparer.Compare(t, keys[j]) < 0)
                {
                    keys[j + 1] = keys[j];
                    if (values != null)
                        values[j + 1] = values[j];
                    j--;
                }
                keys[j + 1] = t;
                if (values != null)
                    values[j + 1] = tValue;
            }
        }
    }
    #endregion
}
