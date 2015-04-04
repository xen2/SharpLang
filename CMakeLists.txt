cmake_minimum_required(VERSION 2.8.12.2)

# Redefine few things to use LLVM compiler instead
if(WIN32)
set(LLVM_BUILD_PATH "${CMAKE_CURRENT_SOURCE_DIR}/deps/llvm/build_x64/RelWithDebInfo/bin")
find_program(CMAKE_MAKE_PROGRAM NAMES mingw32-make PATHS "${CMAKE_CURRENT_SOURCE_DIR}/deps/mingw64/bin" NO_DEFAULT_PATH)
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/deps/mingw64/x86_64-w64-mingw32/include)
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/deps/mingw64/x86_64-w64-mingw32/include/c++)
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/deps/mingw64/x86_64-w64-mingw32/include/c++/x86_64-w64-mingw32)
set(SHARPLANG_TARGET   "x86_64-pc-windows-gnu")
else()
set(LLVM_BUILD_PATH "${CMAKE_CURRENT_SOURCE_DIR}/deps/llvm/build_x64/bin")
set(SHARPLANG_TARGET   "x86_64-linux-gnu")
endif()

find_program(CMAKE_C_COMPILER NAMES clang PATHS "${LLVM_BUILD_PATH}" NO_DEFAULT_PATH)
find_program(CMAKE_CXX_COMPILER NAMES clang++ PATHS "${LLVM_BUILD_PATH}" NO_DEFAULT_PATH)
find_program(CMAKE_LINKER NAMES llvm-link PATHS "${LLVM_BUILD_PATH}" NO_DEFAULT_PATH)
find_program(CMAKE_CXX_LINKER NAMES llvm-link PATHS "${LLVM_BUILD_PATH}" NO_DEFAULT_PATH)
find_program(CMAKE_C_LINK_EXECUTABLE NAMES llvm-link PATHS "${LLVM_BUILD_PATH}" NO_DEFAULT_PATH)
find_program(CMAKE_CXX_LINK_EXECUTABLE NAMES llvm-link PATHS "${LLVM_BUILD_PATH}" NO_DEFAULT_PATH)

set(SHARPLANG_BUILD_SUBDIR "${CMAKE_BINARY_DIR}/../../lib")

# Set default compilation flags
set(CMAKE_BITCODE_FLAGS   "${CMAKE_BITCODE_FLAGS} -emit-llvm --target=${SHARPLANG_TARGET}")

# Set bitcode output directory
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${SHARPLANG_BUILD_SUBDIR}/runtime/${SHARPLANG_TARGET})

set(CMAKE_C_COMPILER_ID "GNU")
set(CMAKE_C_COMPILER_ID_RUN TRUE)
set(CMAKE_C_COMPILER_FORCED TRUE)
set(CMAKE_CXX_COMPILER_ID "GNU")
set(CMAKE_CXX_COMPILER_ID_RUN TRUE)
set(CMAKE_CXX_COMPILER_FORCED TRUE)
set(CMAKE_COMPILER_IS_GNUCC 1)
set(CMAKE_COMPILER_IS_GNUCXX 1)

# Not sure why those flags need to be defined after project
set(CMAKE_C_ARCHIVE_CREATE "${CMAKE_C_LINK_EXECUTABLE} -o <TARGET> <LINK_FLAGS> <OBJECTS>")
set(CMAKE_CXX_ARCHIVE_CREATE "${CMAKE_CXX_LINK_EXECUTABLE} -o <TARGET> <LINK_FLAGS> <OBJECTS>")
set(CMAKE_C_ARCHIVE_FINISH "")
set(CMAKE_CXX_ARCHIVE_FINISH "")

project(SharpLang)

set(CMAKE_C_OUTPUT_EXTENSION ".bc")
set(CMAKE_CXX_OUTPUT_EXTENSION ".bc")
set(CMAKE_STATIC_LIBRARY_SUFFIX ".bc")
set(CMAKE_STATIC_LIBRARY_PREFIX "")

add_subdirectory(src)
