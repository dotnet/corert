#include <stdio.h>
#include "loadlibrary.c"

int main()
{

    // Sum two integers
    int sum = callSumFunc("../bin/Debug/netstandard2.0/linux-x64/native/NativeCallable.so", "sum", 2, 8);
    printf("The sum is %d \n", sum);

    //Concatenate two strings
    char *sumstring = callSumStringFunc("../bin/Debug/netstandard2.0/linux-x64/native/NativeCallable.so", "sumstring", "ok", "ko");
    printf("The concatenated string is %s \n", sumstring);
}
