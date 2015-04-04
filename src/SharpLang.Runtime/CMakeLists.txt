cmake_minimum_required(VERSION 2.8.12.2)

project(SharpLang.Runtime)

add_definitions(-D__STDC_CONSTANT_MACROS)
add_definitions(-D__STDC_LIMIT_MACROS)
add_definitions(-DLIBCXXABI_HAS_NO_THREADS)
add_definitions(-D_BLD_CLR)
add_definitions(-DFEATURE_CORECLR)
add_definitions(-DFEATURE_BCL_FORMATTING)
add_definitions(-DFEATURE_COREFX_GLOBALIZATION)

# Detect CPU target
if(CMAKE_SYSTEM_PROCESSOR STREQUAL "x86_64" OR CMAKE_SYSTEM_PROCESSOR STREQUAL "AMD64")
  add_definitions(-D_TARGET_ARM64_)
  set(SHARPLANG_RUNTIME_64BIT 1)
elseif(CMAKE_SYSTEM_PROCESSOR STREQUAL "x86")
  add_definitions(-D_TARGET_X86_)
else()
  message(FATAL_ERROR "Unknown CMAKE_SYSTEM_PROCESSOR: ${CMAKE_SYSTEM_PROCESSOR}")
endif()

set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11")

if(NOT WIN32)
  add_subdirectory(coreclr/pal)
endif()

set(CMAKE_C_FLAGS   "${CMAKE_C_FLAGS} ${CMAKE_BITCODE_FLAGS}")
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${CMAKE_BITCODE_FLAGS}")

add_definitions(-DUNICODE)
add_subdirectory(coreclr)

add_definitions(-DUSE_STL)

include_directories("${PROJECT_SOURCE_DIR}/../../deps/llvm/include")
if(SHARPLANG_RUNTIME_64BIT)
  include_directories("${PROJECT_SOURCE_DIR}/../../deps/llvm/build_x64/include")
  add_definitions(-DBIT64)
else()
  include_directories("${PROJECT_SOURCE_DIR}/../../deps/llvm/build_x32/include")
endif()

add_library(SharpLang.Runtime
  STATIC
  ConvertUTF.c
  Exception.cpp
  Internal.cpp
  Marshal.cpp
  RuntimeType.cpp
  ${PROJECT_SOURCE_DIR}/../../deps/libcxxabi/src/abort_message.cpp
  ${PROJECT_SOURCE_DIR}/../../deps/libcxxabi/src/cxa_guard.cpp
  ${PROJECT_SOURCE_DIR}/../../deps/compiler-rt/lib/builtins/mulodi4.c
  )

