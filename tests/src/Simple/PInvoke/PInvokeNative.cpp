// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <stdlib.h>
#include <stdio.h>
#ifdef Windows_NT
#include <windows.h>
#define DLL_EXPORT extern "C" __declspec(dllexport)
#else
#include<errno.h>
#define HANDLE size_t
#define DLL_EXPORT extern "C" __attribute((visibility("default")))
#endif

#if !defined(__stdcall)
#define __stdcall
#endif

DLL_EXPORT int __stdcall Square(int intValue)
{
    return intValue * intValue;
}

DLL_EXPORT int __stdcall IsTrue(bool value)
{
    if (value == true)
        return 1;
    return 0;
}

DLL_EXPORT int __stdcall CheckIncremental(int *array, int sz)
{
    if (array == NULL)
        return 1;

    for (int i = 0; i < sz; i++)
    {
        if (array[i] != i)
            return 1;
    }
    return 0;
}

struct Foo
{
    int a;
    float b;
};

DLL_EXPORT int __stdcall CheckIncremental_Foo(Foo *array, int sz)
{
    if (array == NULL)
        return 1;

    for (int i = 0; i < sz; i++)
    {
        if (array[i].a != i || array[i].b != i)
            return 1;
    }
    return 0;
}  

DLL_EXPORT int __stdcall Inc(int *val)
{
    if (val == NULL)
        return -1;

    *val = *val + 1;
    return 0;
}

DLL_EXPORT int __stdcall VerifyByRefFoo(Foo *val)
{
    if (val->a != 10)
        return -1;
    if (val->b != 20)
        return -1;

    val->a++;
    val->b++;

    return 0;
}    

DLL_EXPORT bool __stdcall GetNextChar(short *value)
{
    if (value == NULL)
        return false;

    *value = *value + 1;
    return true;
}

int CompareAnsiString(const char *val, const char * expected)
{
    if (val == NULL)
        return 0;

    const char *p = expected;
    const char *q = val;

    while (*p  && *q && *p == *q)
    {
        p++;
        q++;
    }

    return *p == 0 && *q == 0;
}

DLL_EXPORT int __stdcall VerifyAnsiString(char *val)
{
    if (val == NULL)
        return 0;

    return CompareAnsiString(val, "Hello World");
}

DLL_EXPORT int __stdcall VerifyAnsiStringArray(char **val)
{
    if (val == NULL || *val == NULL)
        return 0;

    return CompareAnsiString(val[0], "Hello") && CompareAnsiString(val[1], "World");
}

void ToUpper(char *val)
{
    if (val == NULL) 
        return;
    char *p = val;
    while (*p != '\0')
    {
        if (*p >= 'a' && *p <= 'z')
        {
            *p = *p - 'a' + 'A';
        }
        p++;
    }
}

DLL_EXPORT void __stdcall ToUpper(char **val)
{
    if (val == NULL)
        return;

    ToUpper(val[0]);
    ToUpper(val[1]);
}

DLL_EXPORT int __stdcall VerifyUnicodeString(unsigned short *val)
{
    if (val == NULL)
        return 0;

    unsigned short expected[] = {'H', 'e', 'l', 'l', 'o', ' ', 'W', 'o', 'r', 'l', 'd', 0};
    unsigned short *p = expected;
    unsigned short *q = val;

    while (*p  && *q && *p == *q)
    {
        p++;
        q++;
    }

    return *p == 0  &&  *q == 0;
}
DLL_EXPORT bool __stdcall VerifySizeParamIndex(unsigned char ** arrByte, unsigned char *arrSize)
{
    *arrSize = 10;
#ifdef Windows_NT
    *arrByte = (unsigned char *)CoTaskMemAlloc(sizeof(unsigned char) * (*arrSize));
#else
    *arrByte = (unsigned char *)malloc(sizeof(unsigned char) * (*arrSize));
#endif
    if (*arrByte == NULL)
        return false;

    for (int i = 0; i < *arrSize; i++)
    {
        (*arrByte)[i] = (unsigned char)i;
    }
    return true;
}

DLL_EXPORT bool __stdcall LastErrorTest()
{
    int lasterror;
#ifdef Windows_NT
    lasterror = GetLastError();
    SetLastError(12345);
#else
    lasterror = errno;
    errno = 12345;
#endif
    return lasterror == 0;
}

DLL_EXPORT void* __stdcall AllocateMemory(int bytes)
{
    void *mem = malloc(bytes);
    return mem;
}

DLL_EXPORT bool __stdcall ReleaseMemory(void *mem)
{
   free(mem);
   return true;
}

DLL_EXPORT bool __stdcall SafeHandleTest(HANDLE sh, long shValue)
{
    return (long)((size_t)(sh)) == shValue;
}

DLL_EXPORT long __stdcall SafeHandleOutTest(HANDLE **sh)
{
    if (sh == NULL) 
        return -1;

    *sh = (HANDLE *)malloc(100);
    return (long)((size_t)(*sh));
}

DLL_EXPORT bool __stdcall ReversePInvoke_Int(int(__stdcall *fnPtr) (int, int, int, int, int, int, int, int, int, int))
{
    return fnPtr(1, 2, 3, 4, 5, 6, 7, 8, 9, 10) == 55;
}

DLL_EXPORT bool __stdcall ReversePInvoke_String(bool(__stdcall *fnPtr) (char *))
{
    char str[] = "Hello World";
    return fnPtr(str);
}

DLL_EXPORT void __stdcall VerifyStringBuilder(unsigned short *val)
{
    char str[] = "Hello World";
    int i;
    for (i = 0; str[i] != '\0'; i++)
        val[i] = (unsigned short)str[i];
    val[i] = 0;
}


DLL_EXPORT int* __stdcall ReversePInvoke_Unused(void(__stdcall *fnPtr) (void))
{
    return 0;
}

struct NativeSequentialStruct
{
    short s;
    int a;
    float b;
    char *str;
};

DLL_EXPORT bool __stdcall StructTest(NativeSequentialStruct nss)
{
    if (nss.s != 100)
        return false;

    if (nss.a != 1)
        return false;

    if (nss.b != 10.0)
       return false;


    if (!CompareAnsiString(nss.str, "Hello"))
        return false;

    return true;
}

DLL_EXPORT void __stdcall StructTest_ByRef(NativeSequentialStruct *nss)
{
    nss->a++;
    nss->b++;

    char *p = nss->str;
    while (*p != NULL)
    {
        *p = *p + 1;
        p++;
    }
}

DLL_EXPORT void __stdcall StructTest_ByOut(NativeSequentialStruct *nss)
{
    nss->s = 1;
    nss->a = 1;
    nss->b = 1.0;

    int arrSize = 7;
    char *p;
#ifdef Windows_NT
    p = (char *)CoTaskMemAlloc(sizeof(char) * arrSize);
#else
    p = (char *)malloc(sizeof(char) * arrSize);
#endif

    for (int i = 0; i < arrSize; i++)
    {
        *(p + i) = i + '0';
    }
    *(p + arrSize) = '\0';
    nss->str = p;
}

typedef struct {
    int a;
    int b;
    int c;
    short inlineArray[128];
    char inlineString[11];
} inlineStruct;

typedef struct {
    int a;
    unsigned short inlineString[11];
} inlineUnicodeStruct;


DLL_EXPORT bool __stdcall InlineArrayTest(inlineStruct* p, inlineUnicodeStruct *q)
{
    for (short i = 0; i < 128; i++)
    {
        if (p->inlineArray[i] != i)
            return false;
        p->inlineArray[i] = i + 1;
    }
    
    if (CompareAnsiString(p->inlineString, "Hello") != 1)
       return false;

    if (!VerifyUnicodeString(q->inlineString))
        return false;

    q->inlineString[5] = p->inlineString[5] = ' ';
    q->inlineString[6] = p->inlineString[6] = 'W';
    q->inlineString[7] = p->inlineString[7] = 'o';
    q->inlineString[8] = p->inlineString[8] = 'r';
    q->inlineString[9] = p->inlineString[9] = 'l';
    q->inlineString[10] = p->inlineString[10] = 'd';

	return true;
}

struct NativeExplicitStruct
{
    int a;
    char padding1[8];
    float b;
    char padding2[8];
    char *str;
};

DLL_EXPORT bool __stdcall StructTest_Explicit(NativeExplicitStruct nes)
{
    if (nes.a != 100)
        return false;

    if (nes.b != 100.0)
        return false;


    if (!CompareAnsiString(nes.str, "Hello"))
        return false;

    return true;
}

struct NativeNestedStruct
{
    int a;
    NativeExplicitStruct nes;
};

DLL_EXPORT bool __stdcall StructTest_Nested(NativeNestedStruct nns)
{
    if (nns.a != 100)
        return false;
    
    return StructTest_Explicit(nns.nes);
}

