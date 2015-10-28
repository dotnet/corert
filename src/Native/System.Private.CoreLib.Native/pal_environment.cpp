#include <stdlib.h>
#include <string.h>
#include <assert.h>

extern "C" int32_t GetEnvironmentVariable(const char* variable, char** result)
{
   assert(result != NULL);

   // Read the environment variable
   *result = getenv(variable);

   if (*result == NULL)
   {
       return 0;
   }
   
   return (int32_t)strlen(*result);
}
