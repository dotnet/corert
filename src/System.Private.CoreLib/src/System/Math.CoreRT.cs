// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
** Purpose: Some floating-point math operations
**
===========================================================*/

//This class contains only static members and doesn't require serialization.

using System.Runtime;
using System.Runtime.CompilerServices;

namespace System
{
    public static partial class Math
    {
        [Intrinsic]
        public static float Abs(float value)
        {
            return (float)RuntimeImports.fabs(value);
        }

        [Intrinsic]
        public static double Abs(double value)
        {
            return RuntimeImports.fabs(value);
        }

        [Intrinsic]
        public static double Acos(double d)
        {
            return RuntimeImports.acos(d);
        }

        [Intrinsic]
        public static double Asin(double d)
        {
            return RuntimeImports.asin(d);
        }

        [Intrinsic]
        public static double Atan(double d)
        {
            return RuntimeImports.atan(d);
        }

        [Intrinsic]
        public static double Atan2(double y, double x)
        {          
            return RuntimeImports.atan2(y, x);
        }

        [Intrinsic]
        public static double Ceiling(double a)
        {
            return RuntimeImports.ceil(a);
        }

        [Intrinsic]
        public static double Cos(double d)
        {
            return RuntimeImports.cos(d);
        }

        [Intrinsic]
        public static double Cosh(double value)
        {
            return RuntimeImports.cosh(value);
        }

        [Intrinsic]
        public static double Exp(double d)
        {  
            return RuntimeImports.exp(d);
        }

        [Intrinsic]
        public static double Floor(double d)
        {
            return RuntimeImports.floor(d);
        }

        [Intrinsic]
        public static double Log(double d)
        {
            return RuntimeImports.log(d);
        }

        [Intrinsic]
        public static double Log10(double d)
        {
            return RuntimeImports.log10(d);
        }

        [Intrinsic]
        public static double Pow(double x, double y)
        {
            return RuntimeImports.pow(x, y);
        }

        [Intrinsic]
        public static double Sin(double a)
        {
            return RuntimeImports.sin(a);
        }

        [Intrinsic]
        public static double Sinh(double value)
        {
            return RuntimeImports.sinh(value);
        }

        [Intrinsic]
        public static double Sqrt(double d)
        {
            return RuntimeImports.sqrt(d);
        }

        [Intrinsic]
        public static double Tan(double a)
        {
            return RuntimeImports.tan(a);
        }

        [Intrinsic]
        public static double Tanh(double value)
        {
            return RuntimeImports.tanh(value);
        }

        [Intrinsic]
        private static double fmod(double x, double y)
        {
            return RuntimeImports.fmod(x, y);
        }

        [Intrinsic]
        private static unsafe double modf(double x, double* intptr)
        {
            return RuntimeImports.modf(x, intptr);
        }
    }
}
