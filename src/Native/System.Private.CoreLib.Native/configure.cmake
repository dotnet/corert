include(CheckCXXSourceCompiles)
include(CheckCXXSourceRuns)
include(CheckLibraryExists)
include(CheckFunctionExists)

check_library_exists(pthread pthread_condattr_setclock "" HAVE_PTHREAD_CONDATTR_SETCLOCK)

check_cxx_source_runs("
#include <stdlib.h>
#include <time.h>
#include <sys/time.h>

int main()
{
  int ret;
  struct timespec ts;
  ret = clock_gettime(CLOCK_MONOTONIC, &ts);

  exit(ret);
}" HAVE_CLOCK_MONOTONIC)

check_cxx_source_runs("
#include <stdlib.h>
#include <time.h>
#include <sys/time.h>

int main()
{
  int ret;
  struct timespec ts;
  ret = clock_gettime(CLOCK_MONOTONIC_COARSE, &ts);

  exit(ret);
}" HAVE_CLOCK_MONOTONIC_COARSE)

check_cxx_source_runs("
#include <stdlib.h>
#include <mach/mach_time.h>

int main()
{
  int ret;
  mach_timebase_info_data_t timebaseInfo;
  ret = mach_timebase_info(&timebaseInfo);
  mach_absolute_time();
  exit(ret);
}" HAVE_MACH_ABSOLUTE_TIME)

set(CMAKE_REQUIRED_LIBRARIES pthread)
check_cxx_source_runs("
#include <stdlib.h>
#include <sched.h>

int main(void)
{
  if (sched_getcpu() >= 0)
  {
    exit(0);
  }
  exit(1);
}" HAVE_SCHED_GETCPU)
set(CMAKE_REQUIRED_LIBRARIES)

check_include_files(gnu/lib-names.h HAVE_GNU_LIBNAMES_H)

check_function_exists(_NSGetEnviron HAVE__NSGETENVIRON)

configure_file(
    ${CMAKE_CURRENT_SOURCE_DIR}/config.h.in
    ${CMAKE_CURRENT_BINARY_DIR}/config.h)
