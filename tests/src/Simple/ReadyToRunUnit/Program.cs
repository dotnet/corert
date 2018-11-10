// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

internal class ClassWithStatic
{
    public const int StaticValue = 0x666;
    
    [ThreadStatic]
    public static int Static = StaticValue;
}

internal class Program
{
    const int LineCountInitialValue = 0x12345678;

    [ThreadStatic]
    private static string TextFileName = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\clientexclusionlist.xml";

    [ThreadStatic]
    private static int LineCount = LineCountInitialValue;

    private static volatile List<string> _passedTests;

    private static List<string> _failedTests;

    private static bool NewString()
    {
        string s = new string('x', 10);
        return s.Length == 10;
    }

    private static bool WriteLine()
    {
        Console.WriteLine("Hello CoreRT R2R running on CoreCLR!");
        return true;
    }
    
    private static bool IsInstanceOf()
    {
        object obj = TextFileName;
        if (obj is string str)
        {
            Console.WriteLine($@"Object is string: {str}");
            return true;
        }
        else
        {
            Console.Error.WriteLine($@"Object is not a string: {obj}");
            return false;
        }
    }

    private static bool IsInstanceOfValueType()
    {
        object obj = LineCount;
        if (obj is int i)
        {
            Console.WriteLine($@"Object {obj:X8} is int: {i:X8}");
            return true;
        }
        else
        {
            Console.Error.WriteLine($@"Object is not an int: {obj}");
            return false;
        }
    }
    
    private unsafe static bool CheckNonGCThreadLocalStatic()
    {
        fixed (int *lineCountPtr = &LineCount)
        {
            Console.WriteLine($@"LineCount: 0x{LineCount:X8}, @ = 0x{(ulong)lineCountPtr:X8}");
        }
        fixed (int *staticPtr = &ClassWithStatic.Static)
        {
            Console.WriteLine($@"ClassWithStatic.Static: 0x{ClassWithStatic.Static:X8}, @ = 0x{(ulong)staticPtr:X8}");
        }
        fixed (int *lineCountPtr = &LineCount)
        {
            Console.WriteLine($@"LineCount: 0x{LineCount:X8}, @ = 0x{(ulong)lineCountPtr:X8}");
        }
        return LineCount == LineCountInitialValue &&
            ClassWithStatic.Static == ClassWithStatic.StaticValue;
    }

    private static bool ChkCast()
    {
        object obj = TextFileName;
        string objString = (string)obj;
        Console.WriteLine($@"String: {objString}");
        return objString == TextFileName;
    }

    private static bool ChkCastValueType()
    {
        object obj = LineCount;
        int objInt = (int)obj;
        Console.WriteLine($@"Int: {objInt:X8}");
        return objInt == LineCount;
    }
    
    private static bool BoxUnbox()
    {
        bool success = true;
        object intAsObject = LineCount;
        int unboxedInt = (int)intAsObject;
        if (unboxedInt == LineCount)
        {
            Console.WriteLine($@"unbox == box: original {LineCount}, boxed {intAsObject:X8}, unboxed {unboxedInt:X8}");
        }
        else
        {
            Console.Error.WriteLine($@"unbox != box: original {LineCount}, boxed {intAsObject:X8}, unboxed {unboxedInt:X8}");
            success = false;
        }
        int? nullableInt = LineCount;
        object nullableIntAsObject = nullableInt;
        int? unboxedNullable = (int?)nullableIntAsObject;
        if (unboxedNullable == nullableInt)
        {
            Console.WriteLine($@"unbox_nullable == box_nullable: original {nullableInt:X8}, boxed {nullableIntAsObject:X8}, unboxed {unboxedNullable:X8}");
        }
        else
        {
            Console.Error.WriteLine($@"unbox_nullable != box_nullable: original {nullableInt:X8}, boxed {nullableIntAsObject:X8}, unboxed {unboxedNullable:X8}");
            success = false;
        }
        return success;
    }
    
    private static bool TypeHandle()
    {
        Console.WriteLine(TextFileName.GetType().ToString());
        Console.WriteLine(LineCount.GetType().ToString());
        return true;
    }

    private static bool RuntimeTypeHandle()
    {
        Console.WriteLine(typeof(string).ToString());
        return true;
    }

    private static bool ReadAllText()
    {
        Console.WriteLine($@"Dumping file: {TextFileName}");
        string textFile = File.ReadAllText(TextFileName);
        if (textFile.Length > 100)
        {
            textFile = textFile.Substring(0, 100) + "...";
        }
        Console.WriteLine(textFile);

        return textFile.Length > 0;
    }

    private static bool StreamReaderReadLine()
    {
        Console.WriteLine($@"Dumping file: {TextFileName}");
        using (StreamReader reader = new StreamReader(TextFileName, System.Text.Encoding.UTF8))
        {
            Console.WriteLine("StreamReader created ...");
            string line1 = reader.ReadLine();
            Console.WriteLine($@"Line 1: {line1}");
            string line2 = reader.ReadLine();
            Console.WriteLine($@"Line 2: {line2}");
            return line2 != null;
        }
    }
    
    private static bool ConstructListOfInt()
    {
        List<int> listOfInt = new List<int>();
        if (listOfInt.Count == 0)
        {
            Console.WriteLine("Successfully constructed empty List<int>!");
            return true;
        }
        else
        {
            Console.WriteLine($@"Invalid element count in List<int>: {listOfInt.Count}");
            return false;
        }
    }
    
    private static bool ManipulateListOfInt()
    {
        List<int> listOfInt = new List<int>();
        const int ItemCount = 100;
        for (int index = ItemCount; index > 0; index--)
        {
            listOfInt.Add(index);
        }
        listOfInt.Sort();
        for (int index = 0; index < listOfInt.Count; index++)
        {
            Console.Write($@"{listOfInt[index]} ");
            if (index > 0 && listOfInt[index] <= listOfInt[index - 1])
            {
                // The list should be monotonically increasing now
                return false;
            }
        }
        Console.WriteLine();
        return listOfInt.Count == ItemCount;
    }

    private static bool ConstructListOfString()
    {
        List<string> listOfString = new List<string>();
        return listOfString.Count == 0;
    }

    private static bool ManipulateListOfString()
    {
        List<string> listOfString = new List<string>();
        const int ItemCount = 100;
        for (int index = ItemCount; index > 0; index--)
        {
            listOfString.Add(index.ToString());
        }
        listOfString.Sort();
        for (int index = 0; index < listOfString.Count; index++)
        {
            Console.Write($@"{listOfString[index]} ");
            if (index > 0 && listOfString[index].CompareTo(listOfString[index - 1]) <= 0)
            {
                // The list should be monotonically increasing now
                return false;
            }
        }
        Console.WriteLine();
        return listOfString.Count == ItemCount;
    }

    private delegate char CharFilterDelegate(char inputChar);

    private static bool SimpleDelegateTest()
    {
        CharFilterDelegate filterDelegate = CharFilterUpperCase;
        char lower = 'x';
        char upper = filterDelegate(lower);
        Console.WriteLine($@"lower = '{lower}', upper = '{upper}'");
        return upper == Char.ToUpper(lower);
    }

    private static bool CharFilterDelegateTest()
    {
        string transformedString = TransformStringUsingCharFilter(TextFileName, CharFilterUpperCase);
        Console.WriteLine(transformedString);
        return transformedString.Length == TextFileName.Length;
    }

    private static string TransformStringUsingCharFilter(string inputString, CharFilterDelegate charFilter)
    {
        StringBuilder outputBuilder = new StringBuilder(inputString.Length);
        foreach (char c in inputString)
        {
            char filteredChar = charFilter(c);
            if (filteredChar != '\0')
            {
                outputBuilder.Append(filteredChar);
            }
        }
        return outputBuilder.ToString();
    }

    private static char CharFilterUpperCase(char c)
    {
        return Char.ToUpperInvariant(c);
    }

    static bool s_sampleActionFlag;

    private static bool ActionTest()
    {
        s_sampleActionFlag = false;
        Action action = SampleAction;
        action();
        return s_sampleActionFlag;
    }

    private static void SampleAction()
    {
        Console.WriteLine("SampleAction() called!");
        s_sampleActionFlag = true;
    }

    private static bool FuncCharCharTest()
    {
        Func<char, char> charFunc = CharFilterUpperCase;

        StringBuilder builder = new StringBuilder();
        foreach (char c in TextFileName)
        {
            builder.Append(charFunc(c));
        }

        Console.WriteLine($@"Func<char,char> string: {builder}");

        return builder.ToString() == TextFileName.ToUpperInvariant();
    }

    class DisposeClass : IDisposable
    {
        public static bool DisposedFlag = false;
        
        public DisposeClass()
        {
            Console.WriteLine("DisposeClass created!");
        }
    
        public void Dispose()
        {
            Console.WriteLine("DisposeClass disposed!");
            DisposedFlag = true;
        }
    }
    
    struct DisposeStruct : IDisposable
    {
        public static bool DisposedFlag = false;

        public void Dispose()
        {
            Console.WriteLine("DisposeStruct disposed!");
            DisposedFlag = true;
        }
    }
    
    private static bool DisposeStructTest()
    {
        using (var disposeStruct = new DisposeStruct())
        {
            Console.WriteLine($@"DisposeStruct test: {disposeStruct}");
        }
        return DisposeStruct.DisposedFlag;
    }

    private static bool DisposeClassTest()
    {
        using (var disposeClass = new DisposeClass())
        {
            Console.WriteLine($@"DisposeClass test: {disposeClass}");
        }
        return DisposeClass.DisposedFlag;
    }
    
    private static bool DisposeEnumeratorTest()
    {
        List<string> listOfString = new List<string>();
        using (var enumerator = listOfString.GetEnumerator())
        {
            Console.WriteLine($@"DisposeEnumeratorTest: {enumerator}");
        }
        return true;
    }

    private static bool EmptyArrayOfInt()
    {
        int[] emptyIntArray = Array.Empty<int>();
        Console.WriteLine("Successfully constructed Array.Empty<int>!");
        return emptyIntArray.Length == 0;
    }

    private static bool EnumerateEmptyArrayOfInt()
    {
        foreach (int element in Array.Empty<int>())
        {
            Console.Error.WriteLine($@"Error: Array.Empty<int> has an element: {element}");
            return false;
        }
        Console.WriteLine("Array.Empty<int> enumeration passed");
        return true;
    }

    private static bool EmptyArrayOfString()
    {
        string[] emptyStringArray = Array.Empty<string>();
        Console.WriteLine("Successfully constructed Array.Empty<string>!");
        return emptyStringArray.Length == 0;
    }

    private static bool EnumerateEmptyArrayOfString()
    {
        foreach (string element in Array.Empty<string>())
        {
            Console.Error.WriteLine($@"Error: Array.Empty<string> has an element: {element}");
            return false;
        }
        Console.WriteLine("Array.Empty<string> enumeration passed");
        return true;
    }
    
    private static bool CreateLocalClassInstance()
    {
        var testClass = new TestClass(1234);
        Console.WriteLine("Successfully constructed TestClass");
        return testClass.A == 1234;
    }

    private class TestClass
    {
        private int _a;

        public TestClass(int a)
        {
            _a = a;
        }

        public int A => _a;
    }

    private static bool TryCatch()
    {
        try
        {
            throw new Exception("Test exception!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"Expected exception: {ex.ToString()}");
            return true;
        }
    }

    private static bool FileStreamNullRefTryCatch()
    {
        try
        {
            FileStream fileStream = new FileStream(null, FileMode.Open, FileAccess.Read);
            Console.Error.WriteLine("Why haven't we thrown an exception?");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"Expected exception: {ex.ToString()}");
            return true;
        }
    }

    class InstanceMethodCaller<T> where T : IComparable
    {
        public static int Compare(T t, object o)
        {
            if ((o is Int32 && 123.CompareTo(o) == 0) ||
                (o is string && "hello".CompareTo(o) == 0))
            {
                return -42;
            }
    
            return t.CompareTo(o);
        }
    }

    private static bool InstanceMethodTest()
    {
        int intResult = InstanceMethodCaller<int>.Compare(122, 123);
        const int ExpectedIntResult = -42;
        Console.WriteLine("Int result: {0}, expected: {1}", intResult, ExpectedIntResult);
        
        int stringResult = InstanceMethodCaller<string>.Compare("hello", "world");
        const int ExpectedStringResult = -1;
        Console.WriteLine("String result: {0}, expected: {1}", stringResult, ExpectedStringResult);
        
        return intResult == ExpectedIntResult && stringResult == ExpectedStringResult;
    }

    class GenericLookup<T>
    {
        public static bool CheckStaticTypeArg(string typeArgName)
        {
            return CompareArgName(typeof(T).ToString(), typeArgName);
        }
        
        public bool CheckInstanceTypeArg(string typeArgName)
        {
            return CompareArgName(typeof(T).ToString(), typeArgName);
        }

        public static bool CheckStaticTypeArg<U>(string tName, string uName)
        {
            return CompareArgName(typeof(T).ToString(), tName) && CompareArgName(typeof(U).ToString(), uName);
        }

        public bool CheckInstanceTypeArg<U>(string tName, string uName)
        {
            return CompareArgName(typeof(T).ToString(), tName) && CompareArgName(typeof(U).ToString(), uName);
        }

        private static bool CompareArgName(string actual, string expected)
        {
            if (actual == expected)
            {
                Console.WriteLine("Arg type match: {0}", actual);
                return true;
            }
            else
            {
                Console.WriteLine("Arg type mismatch: actual = {0}, expected = {1}", actual, expected);
                return false;
            }
        }
    }

    struct GenericStruct<T>
    {
        public T FieldOfT;
        
        public GenericStruct(T fieldOfT)
        {
            FieldOfT = fieldOfT;
        }
    }
    
    class GenericClass<T>
    {
        public T FieldOfT;
        
        public GenericClass(T fieldOfT)
        {
            FieldOfT = fieldOfT;
        }
    }
    
    private static bool ThisObjGenericLookupTest()
    {
        Console.WriteLine("ThisObjGenericLookup:");
        bool result = true;
        result &= (new GenericLookup<object>()).CheckInstanceTypeArg("System.Object");
        result &= (new GenericLookup<string>()).CheckInstanceTypeArg("System.String");
        result &= (new GenericLookup<int>()).CheckInstanceTypeArg("System.Int32");
        result &= (new GenericLookup<GenericStruct<object>>()).CheckInstanceTypeArg("Program+GenericStruct`1[System.Object]");
        result &= (new GenericLookup<GenericStruct<string>>()).CheckInstanceTypeArg("Program+GenericStruct`1[System.String]");
        result &= (new GenericLookup<GenericStruct<int>>()).CheckInstanceTypeArg("Program+GenericStruct`1[System.Int32]");
        result &= (new GenericLookup<GenericClass<object>>()).CheckInstanceTypeArg("Program+GenericClass`1[System.Object]");
        result &= (new GenericLookup<GenericClass<string>>()).CheckInstanceTypeArg("Program+GenericClass`1[System.String]");
        result &= (new GenericLookup<GenericClass<int>>()).CheckInstanceTypeArg("Program+GenericClass`1[System.Int32]");
        return result;
    }

    private static bool ClassParamGenericLookupTest()
    {
        Console.WriteLine("ClassParamGenericLookup:");
        bool result = true;
        result &= GenericLookup<object>.CheckStaticTypeArg("System.Object");
        result &= GenericLookup<string>.CheckStaticTypeArg("System.String");
        result &= GenericLookup<int>.CheckStaticTypeArg("System.Int32");
        result &= GenericLookup<GenericStruct<object>>.CheckStaticTypeArg("Program+GenericStruct`1[System.Object]");
        result &= GenericLookup<GenericStruct<string>>.CheckStaticTypeArg("Program+GenericStruct`1[System.String]");
        result &= GenericLookup<GenericStruct<int>>.CheckStaticTypeArg("Program+GenericStruct`1[System.Int32]");
        result &= GenericLookup<GenericClass<object>>.CheckStaticTypeArg("Program+GenericClass`1[System.Object]");
        result &= GenericLookup<GenericClass<string>>.CheckStaticTypeArg("Program+GenericClass`1[System.String]");
        result &= GenericLookup<GenericClass<int>>.CheckStaticTypeArg("Program+GenericClass`1[System.Int32]");
        return result;
    }

    private static bool MethodParamGenericLookupTest()
    {
        Console.WriteLine("MethodParamGenericLookup:");
        bool result = true;

        result &= GenericLookup<object>.CheckStaticTypeArg<int>("System.Object", "System.Int32");
        result &= GenericLookup<string>.CheckStaticTypeArg<object>("System.String", "System.Object");
        result &= GenericLookup<int>.CheckStaticTypeArg<string>("System.Int32", "System.String");

        result &= GenericLookup<GenericStruct<object>>.CheckStaticTypeArg<GenericStruct<int>>(
            "Program+GenericStruct`1[System.Object]", "Program+GenericStruct`1[System.Int32]");
        result &= GenericLookup<GenericStruct<string>>.CheckStaticTypeArg<GenericStruct<object>>(
            "Program+GenericStruct`1[System.String]", "Program+GenericStruct`1[System.Object]");
        result &= GenericLookup<GenericStruct<int>>.CheckStaticTypeArg<GenericStruct<string>>(
            "Program+GenericStruct`1[System.Int32]", "Program+GenericStruct`1[System.String]");

        result &= GenericLookup<GenericClass<object>>.CheckStaticTypeArg<GenericClass<int>>(
            "Program+GenericClass`1[System.Object]", "Program+GenericClass`1[System.Int32]");
        result &= GenericLookup<GenericClass<string>>.CheckStaticTypeArg<GenericClass<object>>(
            "Program+GenericClass`1[System.String]", "Program+GenericClass`1[System.Object]");
        result &= GenericLookup<GenericClass<int>>.CheckStaticTypeArg<GenericClass<string>>(
            "Program+GenericClass`1[System.Int32]", "Program+GenericClass`1[System.String]");

        result &= GenericLookup<GenericClass<object>>.CheckStaticTypeArg<GenericStruct<int>>(
            "Program+GenericClass`1[System.Object]", "Program+GenericStruct`1[System.Int32]");
        result &= GenericLookup<GenericClass<string>>.CheckStaticTypeArg<GenericStruct<object>>(
            "Program+GenericClass`1[System.String]", "Program+GenericStruct`1[System.Object]");
        result &= GenericLookup<GenericClass<int>>.CheckStaticTypeArg<GenericStruct<string>>(
            "Program+GenericClass`1[System.Int32]", "Program+GenericStruct`1[System.String]");

        result &= GenericLookup<GenericStruct<object>>.CheckStaticTypeArg<GenericClass<int>>(
            "Program+GenericStruct`1[System.Object]", "Program+GenericClass`1[System.Int32]");
        result &= GenericLookup<GenericStruct<string>>.CheckStaticTypeArg<GenericClass<object>>(
            "Program+GenericStruct`1[System.String]", "Program+GenericClass`1[System.Object]");
        result &= GenericLookup<GenericStruct<int>>.CheckStaticTypeArg<GenericClass<string>>(
            "Program+GenericStruct`1[System.Int32]", "Program+GenericClass`1[System.String]");

        result &= (new GenericLookup<object>()).CheckInstanceTypeArg<GenericStruct<int>>(
            "System.Object", "Program+GenericStruct`1[System.Int32]");
        result &= (new GenericLookup<string>()).CheckInstanceTypeArg<GenericStruct<object>>(
            "System.String", "Program+GenericStruct`1[System.Object]");
        result &= (new GenericLookup<int>()).CheckInstanceTypeArg<GenericStruct<string>>(
            "System.Int32", "Program+GenericStruct`1[System.String]");
        result &= (new GenericLookup<GenericStruct<object>>()).CheckInstanceTypeArg<int>(
            "Program+GenericStruct`1[System.Object]", "System.Int32");
        result &= (new GenericLookup<GenericStruct<string>>()).CheckInstanceTypeArg<object>(
            "Program+GenericStruct`1[System.String]", "System.Object");
        result &= (new GenericLookup<GenericStruct<int>>()).CheckInstanceTypeArg<string>(
            "Program+GenericStruct`1[System.Int32]", "System.String");

        result &= (new GenericLookup<object>()).CheckInstanceTypeArg<GenericClass<int>>(
            "System.Object", "Program+GenericClass`1[System.Int32]");
        result &= (new GenericLookup<string>()).CheckInstanceTypeArg<GenericClass<object>>(
            "System.String", "Program+GenericClass`1[System.Object]");
        result &= (new GenericLookup<int>()).CheckInstanceTypeArg<GenericClass<string>>(
            "System.Int32", "Program+GenericClass`1[System.String]");
        result &= (new GenericLookup<GenericClass<object>>()).CheckInstanceTypeArg<int>(
            "Program+GenericClass`1[System.Object]", "System.Int32");
        result &= (new GenericLookup<GenericClass<string>>()).CheckInstanceTypeArg<object>(
            "Program+GenericClass`1[System.String]", "System.Object");
        result &= (new GenericLookup<GenericClass<int>>()).CheckInstanceTypeArg<string>(
            "Program+GenericClass`1[System.Int32]", "System.String");

        return result;
    }

    private static bool VectorTestOf<T>(T value1, T value2, T sum)
        where T : struct
    {
        Console.WriteLine("Constructing vector of {0}", value1);
        Vector<T> vector1 = new Vector<T>(value1);
        Console.WriteLine("Vector[0] = {0}", vector1[0]);
        Vector<T> vector2 = new Vector<T>(value2);
        Vector<T> vectorSum = vector1 + vector2;
        Console.WriteLine("Vector sum = {0}, {1} expected", vectorSum[0], sum);
        return vectorSum[0].Equals(sum);
    }

    private static bool VectorTest()
    {
        bool success = true;
        success &= VectorTestOf<int>(10, 20, 30);
        success &= VectorTestOf<float>(15.0f, 30.0f, 45.0f);
        success &= VectorTestOf<double>(50.0, 100.0, 150.0);
        return success;
    }

    public static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            TextFileName = args[0];
        }

        _passedTests = new List<string>();
        _failedTests = new List<string>();

        RunTest("NewString", NewString());
        RunTest("WriteLine", WriteLine());
        RunTest("IsInstanceOf", IsInstanceOf());
        RunTest("IsInstanceOfValueType", IsInstanceOfValueType());
        RunTest("CheckNonGCThreadLocalStatic", CheckNonGCThreadLocalStatic());
        RunTest("ChkCast", ChkCast());
        RunTest("ChkCastValueType", ChkCastValueType());
        RunTest("BoxUnbox", BoxUnbox());
        RunTest("TypeHandle", TypeHandle());
        RunTest("RuntimeTypeHandle", RuntimeTypeHandle());
        RunTest("ReadAllText", ReadAllText());
        RunTest("StreamReaderReadLine", StreamReaderReadLine());
        RunTest("SimpleDelegateTest", SimpleDelegateTest());
        RunTest("CharFilterDelegateTest", CharFilterDelegateTest());
        RunTest("ActionTest", ActionTest());
        RunTest("FuncCharCharTest", FuncCharCharTest());
        RunTest("ConstructListOfInt", ConstructListOfInt());
        RunTest("ManipulateListOfInt", ManipulateListOfInt());
        RunTest("ConstructListOfString", ConstructListOfString());
        RunTest("ManipulateListOfString", ManipulateListOfString());
        RunTest("CreateLocalClassInstance", CreateLocalClassInstance());
        RunTest("DisposeStructTest", DisposeStructTest());
        RunTest("DisposeClassTest", DisposeClassTest());
        RunTest("DisposeEnumeratorTest", DisposeEnumeratorTest());
        RunTest("EmptyArrayOfInt", EmptyArrayOfInt());
        RunTest("EnumerateEmptyArrayOfInt", EnumerateEmptyArrayOfInt());
        RunTest("EmptyArrayOfString", EmptyArrayOfString());
        RunTest("EnumerateEmptyArrayOfString", EnumerateEmptyArrayOfString());
        RunTest("TryCatch", TryCatch());
        RunTest("FileStreamNullRefTryCatch", FileStreamNullRefTryCatch());
        RunTest("InstanceMethodTest", InstanceMethodTest());
        RunTest("ThisObjGenericLookupTest", ThisObjGenericLookupTest());
        RunTest("ClassParamGenericLookupTest", ClassParamGenericLookupTest());
        RunTest("MethodParamGenericLookupTest", MethodParamGenericLookupTest());
        RunTest("VectorTest", VectorTest());

        Console.WriteLine($@"{_passedTests.Count} tests pass:");
        foreach (string testName in _passedTests)
        {
            Console.WriteLine($@"    {testName}");
        }

        if (_failedTests.Count == 0)
        {
            Console.WriteLine($@"All {_passedTests.Count} tests pass!");
            return 100;
        }
        else
        {
            Console.Error.WriteLine($@"{_failedTests.Count} test failed:");
            foreach (string testName in _failedTests)
            {
                Console.Error.WriteLine($@"    {testName}");
            }
            return 1;
        }
    }

    private static void RunTest(string name, bool result)
    {
        if (result)
        {
            _passedTests.Add(name);
        }
        else
        {
            _failedTests.Add(name);
        }
    }
}
