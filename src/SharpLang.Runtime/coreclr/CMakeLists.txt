if(WIN32)
  # Windows
  add_definitions(-DFEATURE_CRYPTO)
  add_definitions(-DUSE_STL)
else()
  # Unix/Linux etc...
  include_directories("pal/inc/rt")
  add_definitions(-D__LINUX__)
  add_definitions(-DPLATFORM_UNIX)
  add_definitions(-DFEATURE_PAL)
  add_definitions(-D_WIN32)
  if(SHARPLANG_RUNTIME_64BIT)
    add_definitions(-D_WIN64)
  endif(SHARPLANG_RUNTIME_64BIT)
endif()

include_directories("inc")
include_directories("pal/inc")

if(NOT WIN32)
    add_subdirectory(palrt)
endif()

include_directories("vm")
include_directories("classlibnative/inc")
include_directories("classlibnative/cryptography")
add_definitions(-DFEATURE_CORECLR)
add_definitions(-DFEATURE_BCL_FORMATTING)
add_definitions(-DFEATURE_COREFX_GLOBALIZATION)

add_library(SharpLang.Runtime.CoreCLR
  STATIC
  vm/binder.cpp
  vm/comutilnative.cpp
  vm/crst.cpp
  vm/excep.cpp
  vm/hosting.cpp
  vm/mscorlib.cpp
  vm/qcall.cpp
  vm/safehandle.cpp
  vm/util.cpp
  utilcode/ex.cpp
  sharplang/CoreCLR.cpp
  sharplang/PInvoke.cpp
  sharplang/sharplang.cpp
  classlibnative/bcltype/console.cpp
  classlibnative/bcltype/currency.cpp
  classlibnative/bcltype/decimal.cpp
  classlibnative/bcltype/number.cpp
  classlibnative/bcltype/oavariant.cpp
  classlibnative/bcltype/variant.cpp
  classlibnative/bcltype/windowsruntimebufferhelper.cpp
  classlibnative/cryptography/cryptography.cpp
  classlibnative/cryptography/x509certificate.cpp
  classlibnative/float/floatnative.cpp
  )