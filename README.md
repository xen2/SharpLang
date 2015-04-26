# SharpLang

[![Join the chat at https://gitter.im/xen2/SharpLang](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/xen2/SharpLang?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

SharpLang is a .NET (MSIL) compiler front-end for LLVM.

It allows you to compile C# and other .NET languages code to any supported LLVM backend.

Official twitter account: https://twitter.com/SharpLangDev

## Licence

BSD 2-clause license.

## Documentation

[Getting Started](docs/GettingStarted.md)

## Objectives

* Fill the gap between managed flexibility/safety/productivity (on by default) and native speed/control (when you actually need it)
* Supports many platforms (including Windows, MacOSX, mobile devices, consoles, browser)
* Performance oriented
 * Lightweight runtime
 * Some .NET features that implies too much overhead for to their actual uses will be emulated or dropped (i.e. array covariance -- unsafe and huge performance implication)
 * Various technical choices to avoid unecessary conversions and interop with other languages
* Provide a good starting point to implement "M#" features as soon as possible (http://joeduffyblog.com/2013/12/27/csharp-for-systems-programming/)
* Flexible and controllable (.NET shouldn't be a black box anymore; optimize what you want, pay for what you actually use, etc...)

## Planned features

It's still a early work in progress. Any help/feedback is welcome!

### What's working

* Codegen (unit tested)
 * Supports most .NET opcodes
 * Branching
 * Classes and interfaces
 * Generics (expanded like C++ template)
 * PInvoke (simple, no marshalling)
 * Delegate (no multicast delegate yet)
 * Boxing
 * Exceptions (DWARF and SEH)
 * Converts huge libraries (i.e. Mono mscorlib, System, System.Core, System.Xml, etc...)
 * Reflection (only types, no method/field yet)
 * x86 and x64
* Simple code linked against SharpLang-compiled Mono mscorlib is starting to work (i.e. WinForms+SharpDX)

### What's next

* Driver/Linker
* Runtime (probably lot will be imported from [CoreCLR](https://github.com/dotnet/coreclr))
 * Reflection (method, fields, custom attributes)
 * Threads, Locks, etc...
 * I/O
 * Marshalling
 * Vararg
* GC integration (either [CoreCLR](https://github.com/dotnet/coreclr/tree/master/src/gc), [Mono](https://github.com/mono/mono/tree/master/mono/metadata) or [BoehmGC](https://github.com/ivmai/bdwgc/))
* Cross-platform
 * [Emscripten](https://github.com/kripken/emscripten) HTML5 (javascript) support
 * Linux/MacOSX support
 * Android, iOS
* Code quality
* Documentation

### Future

* Dynamic loading
* Performance extensions
 * Non-nullable types
 * SIMD support
 * More control over class/struct layouting (allocate class on stack, etc...)
* Various "M#" improvements
 * Uniqueness and Reference Immutability for Safe Parallelism: http://research.microsoft.com/pubs/170528/msr-tr-2012-79.pdf
 * Lifetime control (unique/owned pointers, stack alloc as much as possible with pure functions, etc...)
 * RAII (struct destructors)
 * Use and extend Roslyn for new language extensions
* Direct (or at least easy) access to C++ (probably with help of libclang)
* Other platforms

## Various technical details

Here for now, until later moved to a more specific documentation.

* ~~UTF8 string (UTF16 was choosed mainly because of Windows API, but UTF8 is more space efficient, API compatible with char* and avoid many conversions)~~
* String/Array indirection to more easily carve substring/subarrays.
