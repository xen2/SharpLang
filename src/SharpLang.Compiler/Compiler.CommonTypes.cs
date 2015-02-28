// Copyright (c) 2014 SharpLang - Virgile Bello

using System;
using System.IO;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        // SharpLang Types
        private Type intPtr;
        private Type int8;
        private Type int16;
        private Type int32;
        private Type int64;
        private Type uint8;
        private Type uint16;
        private Type uint32;
        private Type uint64;
        private Type @bool;
        private Type @float;
        private Type @double;
        private Type @char;
        private Type @object;
        private Type @void;
        private Type fieldDesc;

        // LLVM Types
        private TypeRef intPtrLLVM; // Native integer, pointer representation
        private int intPtrSize;
        private TypeRef nativeIntLLVM; // Native integer, integer representation
        private TypeRef int16LLVM;
        private TypeRef int32LLVM;
        private TypeRef int64LLVM;

        // Runtime
        private ModuleRef runtimeModule;

        // Runtime Types
        private TypeRef imtEntryLLVM;
        private TypeRef caughtResultLLVM;

        // Runtime Methods
        private ValueRef allocObjectFunctionLLVM;
        private ValueRef resolveInterfaceCallFunctionLLVM;
        private ValueRef isInstInterfaceFunctionLLVM;
        private ValueRef throwExceptionFunctionLLVM;
        private ValueRef sharpPersonalityFunctionLLVM;
        private ValueRef pinvokeLoadLibraryFunctionLLVM;
        private ValueRef pinvokeGetProcAddressFunctionLLVM;

        // Types used for reflection
        private TypeRef typeDefLLVM;
        private Type sharpLangTypeType;
        private Type sharpLangModuleType;

        static string LocateRuntimeModuleHelper(string triple)
        {
            return string.Format(@"..\runtime\{0}\SharpLang.Runtime.bc", triple).Replace('\\', Path.DirectorySeparatorChar);
        }

        public static string LocateRuntimeModule(string triple)
        {
            // Locate runtime
            var runtimeLocation = LocateRuntimeModuleHelper(triple);
            if (!File.Exists(runtimeLocation))
                runtimeLocation = LocateRuntimeModuleHelper(triple.Replace("-unknown", string.Empty));
            if (!File.Exists(runtimeLocation))
                throw new InvalidOperationException(string.Format("Can't locate runtime for target {0}", triple));

            return runtimeLocation;
        }

        public void InitializeCommonTypes()
        {
            // Load runtime
            runtimeModule = LoadModule(context, LocateRuntimeModule(triple));

            // Load data layout from runtime
            var dataLayout = LLVM.GetDataLayout(runtimeModule);
            targetData = LLVM.CreateTargetData(dataLayout);

            // Initialize LLVM types
            intPtrLLVM = LLVM.PointerType(LLVM.Int8TypeInContext(context), 0);
            int16LLVM = LLVM.Int16TypeInContext(context);
            int32LLVM = LLVM.Int32TypeInContext(context);
            int64LLVM = LLVM.Int64TypeInContext(context);
            intPtrSize = (int)LLVM.ABISizeOfType(targetData, intPtrLLVM);
            nativeIntLLVM = LLVM.IntTypeInContext(context, (uint)intPtrSize * 8);

            // Prepare system types, for easier access
            intPtr = GetType(corlib.MainModule.GetType(typeof(IntPtr).FullName), TypeState.StackComplete);
            int8 = GetType(corlib.MainModule.GetType(typeof(sbyte).FullName), TypeState.StackComplete);
            int16 = GetType(corlib.MainModule.GetType(typeof(short).FullName), TypeState.StackComplete);
            int32 = GetType(corlib.MainModule.GetType(typeof(int).FullName), TypeState.StackComplete);
            int64 = GetType(corlib.MainModule.GetType(typeof(long).FullName), TypeState.StackComplete);
            uint8 = GetType(corlib.MainModule.GetType(typeof(byte).FullName), TypeState.StackComplete);
            uint16 = GetType(corlib.MainModule.GetType(typeof(ushort).FullName), TypeState.StackComplete);
            uint32 = GetType(corlib.MainModule.GetType(typeof(uint).FullName), TypeState.StackComplete);
            uint64 = GetType(corlib.MainModule.GetType(typeof(ulong).FullName), TypeState.StackComplete);
            @bool = GetType(corlib.MainModule.GetType(typeof(bool).FullName), TypeState.StackComplete);
            @float = GetType(corlib.MainModule.GetType(typeof(float).FullName), TypeState.StackComplete);
            @double = GetType(corlib.MainModule.GetType(typeof(double).FullName), TypeState.StackComplete);
            @char = GetType(corlib.MainModule.GetType(typeof(char).FullName), TypeState.StackComplete);
            @object = GetType(corlib.MainModule.GetType(typeof(object).FullName), TypeState.StackComplete);
            @void = GetType(corlib.MainModule.GetType(typeof(void).FullName), TypeState.StackComplete);
            fieldDesc = GetType(corlib.MainModule.GetType("System.SharpLangFieldDescription"), TypeState.StackComplete);

            // struct IMTEntry { i8* interfaceFunctionPtr, i8* functionPtr }
            imtEntryLLVM = LLVM.StructCreateNamed(context, "IMTEntry");
            LLVM.StructSetBody(imtEntryLLVM, new[] { intPtrLLVM, intPtrLLVM }, false);

            // struct CaughtResultType { i8*, i32 }
            caughtResultLLVM = LLVM.StructCreateNamed(context, "CaughtResultType");
            LLVM.StructSetBody(caughtResultLLVM, new[] { intPtrLLVM, int32LLVM }, false);

            // Prepare types used to emit metadata and reflection
            if (!TestMode)
            {
                sharpLangTypeType = GetType(corlib.MainModule.GetType("System.SharpLangType"), TypeState.StackComplete);
                sharpLangModuleType = GetType(corlib.MainModule.GetType("System.SharpLangModule"), TypeState.StackComplete);
            }
            else
            {
                sharpLangTypeType = intPtr;
                sharpLangModuleType = intPtr;
            }

            // struct TypeDef { SharpLangModule*, i32 }
            typeDefLLVM = LLVM.StructCreateNamed(context, "TypeDef");
            LLVM.StructSetBody(typeDefLLVM, new[] { sharpLangModuleType.DefaultTypeLLVM, int32LLVM }, false);

            // Import runtime methods
            allocObjectFunctionLLVM = ImportRuntimeFunction(module, "allocObject");
            resolveInterfaceCallFunctionLLVM = ImportRuntimeFunction(module, "resolveInterfaceCall");
            isInstInterfaceFunctionLLVM = ImportRuntimeFunction(module, "isInstInterface");
            throwExceptionFunctionLLVM = ImportRuntimeFunction(module, "throwException");
            sharpPersonalityFunctionLLVM = ImportRuntimeFunction(module, "sharpPersonality");
            pinvokeLoadLibraryFunctionLLVM = ImportRuntimeFunction(module, "PInvokeOpenLibrary");
            pinvokeGetProcAddressFunctionLLVM = ImportRuntimeFunction(module, "PInvokeGetProcAddress");
        }

        private static ModuleRef LoadModule(ContextRef context, string fileName)
        {
            MemoryBufferRef memoryBuffer;
            string message;
            if (LLVM.CreateMemoryBufferWithContentsOfFile(fileName, out memoryBuffer, out message))
                throw new InvalidOperationException(message);

            ModuleRef runtimeModule;
            if (LLVM.GetBitcodeModuleInContext(context, memoryBuffer, out runtimeModule, out message))
                throw new InvalidOperationException(message);

            LLVM.DisposeMemoryBuffer(memoryBuffer);

            return runtimeModule;
        }

        private ValueRef ImportRuntimeFunction(ModuleRef module, string name)
        {
            var function = LLVM.GetNamedFunction(runtimeModule, name);
            var functionType = LLVM.GetElementType(LLVM.TypeOf(function));

            return LLVM.AddFunction(module, name, functionType);
        }
    }
}