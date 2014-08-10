using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Provides access to C# LLVM code generator.
    /// </summary>
    public sealed partial class Compiler
    {
        private ModuleRef module;
        private ContextRef context;

        // RuntimeInline
        private ValueRef allocObjectFunction;
        private ValueRef resolveInterfaceCallFunction;
        private ValueRef isInstInterfaceFunction;
        private ValueRef throwExceptionFunction;
        private ValueRef sharpPersonalityFunction;

        // Builder for normal instructions
        private BuilderRef builder;
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
        private Type @object;
        private TypeRef intPtrType; // Native integer, pointer representation
        private int intPtrSize;
        private TypeRef nativeIntType; // Native integer, integer representation
        private TypeRef int32Type;
        private TypeRef int64Type;
        private TypeRef imtEntryType;
        private TypeRef caughtResultType;

        // Builder that is used for PHI nodes
        private BuilderRef builder2;

        private AssemblyDefinition assembly;
        private AssemblyDefinition corlib;

        private Dictionary<TypeReference, Type> types = new Dictionary<TypeReference, Type>(MemberEqualityComparer.Default);
        private Dictionary<MethodReference, Function> functions = new Dictionary<MethodReference, Function>(MemberEqualityComparer.Default);
        private Queue<Type> classesToGenerate = new Queue<Type>();

        private Queue<KeyValuePair<MethodReference, Function>> methodsToCompile = new Queue<KeyValuePair<MethodReference, Function>>();

        public void RegisterMainAssembly(AssemblyDefinition assembly)
        {
            this.assembly = assembly;
            corlib = assembly.MainModule.Import(typeof (void)).Resolve().Module.Assembly;
            module = LLVM.ModuleCreateWithName(assembly.Name.Name);

            // TODO: Choose appropriate triple depending on target
            LLVM.SetTarget(module, "i686-pc-mingw32");

            RuntimeInline.Runtime.makeLLVMModuleContents(module);
            allocObjectFunction = LLVM.GetNamedFunction(module, "allocObject");
            resolveInterfaceCallFunction = LLVM.GetNamedFunction(module, "resolveInterfaceCall");
            isInstInterfaceFunction = LLVM.GetNamedFunction(module, "isInstInterface");
            throwExceptionFunction = LLVM.GetNamedFunction(module, "throwException");
            sharpPersonalityFunction = LLVM.GetNamedFunction(module, "sharpPersonality");

            context = LLVM.GetModuleContext(module);
            builder = LLVM.CreateBuilderInContext(context);
            intPtrType = LLVM.PointerType(LLVM.Int8TypeInContext(context), 0);
            int32Type = LLVM.Int32TypeInContext(context);
            int64Type = LLVM.Int64TypeInContext(context);
            intPtrSize = 4;            // Or 8?
            nativeIntType = int32Type; // Or int64Type?
            builder2 = LLVM.CreateBuilderInContext(context);

            intPtr = GetType(corlib.MainModule.GetType(typeof(IntPtr).FullName));
            int8 = GetType(corlib.MainModule.GetType(typeof(sbyte).FullName));
            int16 = GetType(corlib.MainModule.GetType(typeof(short).FullName));
            int32 = GetType(corlib.MainModule.GetType(typeof(int).FullName));
            int64 = GetType(corlib.MainModule.GetType(typeof(long).FullName));
            uint8 = GetType(corlib.MainModule.GetType(typeof(byte).FullName));
            uint16 = GetType(corlib.MainModule.GetType(typeof(ushort).FullName));
            uint32 = GetType(corlib.MainModule.GetType(typeof(uint).FullName));
            uint64 = GetType(corlib.MainModule.GetType(typeof(ulong).FullName));
            @bool = GetType(corlib.MainModule.GetType(typeof(bool).FullName));
            @float = GetType(corlib.MainModule.GetType(typeof(float).FullName));
            @double = GetType(corlib.MainModule.GetType(typeof(double).FullName));

            @object = GetType(corlib.MainModule.GetType(typeof(object).FullName));

            // struct IMTEntry { i32 functionId, i8* functionPtr }
            imtEntryType = LLVM.StructCreateNamed(context, "IMTEntry");
            LLVM.StructSetBody(imtEntryType, new[] { int32Type, intPtrType }, false);

            caughtResultType = LLVM.StructCreateNamed(context, "CaughtResultType");
            LLVM.StructSetBody(caughtResultType, new[] { intPtrType, int32Type }, false);

            RegisterAssembly(assembly);
        }

        public void RegisterType(TypeReference typeReference)
        {
            var type = CreateType(typeReference);
            EmitType(type);
            BuildRuntimeType(GetClass(type));
        }

        public void RegisterAssembly(AssemblyDefinition assembly)
        {
            // Process types
            foreach (var assemblyModule in assembly.Modules)
            {
                var typeReferences = assemblyModule.GetTypeReferences();
                foreach (var type in typeReferences)
                {
                    CreateType(type);
                }

                var memberReferences = assemblyModule.GetMemberReferences();
                foreach (var member in memberReferences)
                {
                    var method = member as MethodReference;
                    if (member.DeclaringType.ContainsGenericParameter())
                        continue;
                    CreateType(member.DeclaringType);
                    if (method != null)
                    {
                        if (!method.HasGenericParameters)
                            CreateFunction(method);
                    }
                }

                foreach (var type in assemblyModule.Types)
                {
                    if (!type.HasGenericParameters && type.FullName != typeof(void).FullName)
                        GetClass(type);

                    foreach (var nestedType in type.NestedTypes)
                    {
                        if (!nestedType.HasGenericParameters)
                            GetClass(nestedType);
                    }
                }
            }
        }

        public ModuleRef GenerateModule()
        {
            // Process methods
            while (classesToGenerate.Count > 0)
            {
                var classToGenerate = classesToGenerate.Dequeue();
                if (classToGenerate.IsLocal)
                {
                    PrepareClassMethods(classToGenerate);
                }
            }

            // Generate code
            while (methodsToCompile.Count > 0)
            {
                var methodToCompile = methodsToCompile.Dequeue();
                Console.WriteLine("Compiling {0}", methodToCompile.Key.FullName);
                CompileFunction(methodToCompile.Key, methodToCompile.Value);
            }

            // Emit "main" which will call the assembly entry point (if any)
            Function entryPoint;
	        if (assembly.EntryPoint != null && functions.TryGetValue(assembly.EntryPoint, out entryPoint))
	        {
                var mainFunctionType = LLVM.FunctionType(int32Type, new TypeRef[0], false);
	            var mainFunction = LLVM.AddFunction(module, "main", mainFunctionType);
                LLVM.SetLinkage(mainFunction, Linkage.ExternalLinkage);
                LLVM.PositionBuilderAtEnd(builder, LLVM.AppendBasicBlockInContext(context, mainFunction, string.Empty));

	            var parameters = (entryPoint.ParameterTypes.Length > 0)
	                ? new[] { LLVM.ConstPointerNull(entryPoint.ParameterTypes[0].DefaultType) }
	                : new ValueRef[0];

                LLVM.BuildCall(builder, entryPoint.GeneratedValue, parameters, string.Empty);
                LLVM.BuildRet(builder, LLVM.ConstInt(int32Type, 0, false));
	        }

            LLVM.DisposeBuilder(builder);

            // Verify module
#if VERIFY_LLVM
            string message;
            if (LLVM.VerifyModule(module, VerifierFailureAction.PrintMessageAction, out message))
            {
                throw new InvalidOperationException(message);
            }
#endif
            
            return module;
        }

        TypeReference GetBaseTypeDefinition(TypeReference typeReference)
        {
            if (typeReference is ArrayType)
            {
                // Return ArrayType
                return corlib.MainModule.GetType(typeof(Array).FullName);
            }

            // Default: resolve to get real type
            return typeReference.Resolve().BaseType;
        }

        /// <summary>
        /// Gets the type definition containing all the methods for the given type.
        /// </summary>
        /// <returns></returns>
        TypeDefinition GetMethodTypeDefinition(TypeReference typeReference)
        {
            if (typeReference is ArrayType)
            {
                // Return ArrayType
                return corlib.MainModule.GetType(typeof(Array).FullName);
            }

            // Default: resolve to get real type
            return typeReference.Resolve();
        }

        void PrepareClassMethods(Type type)
        {
            var @class = GetClass(type);

            // Already processed?
            if (@class == null || @class.MethodCompiled)
                return;

            @class.MethodCompiled = true;

            // Array: no need to do anything (its type definition, Array, has already been processed)
            if (@class.Type.TypeReference is ArrayType)
                return;

            var typeDefinition = GetMethodTypeDefinition(@class.Type.TypeReference);

            bool isInterface = typeDefinition.IsInterface;

            // Process methods
            foreach (var method in typeDefinition.Methods)
            {
                var methodReference = ResolveGenericMethod(@class.Type.TypeReference, method);

                // If a method contains generic parameters, skip it
                // Its closed instantiations (with generic arguments) is what needs to be generated.
                // (except interface methods)
                // Using ResolveGenericsVisitor.ContainsGenericParameters because Cecil one's doesn't seem to match what .NET Type does.
                // TODO: Might need a more robust generic resolver/analyzer system soon.
                if (ResolveGenericsVisitor.ContainsGenericParameters(methodReference))
                    continue;

                var function = CreateFunction(methodReference);

                @class.Functions.Add(function);

                if (method.IsVirtual)
                {
                    if (isInterface)
                    {
                        // Store IMT slot
                        function.VirtualSlot = (int)(GetMethodId(methodReference) % InterfaceMethodTableSize);
                    }
                    else if (method.IsNewSlot)
                    {
                        // New slot
                        function.VirtualSlot = @class.VirtualTable.Count;
                        @class.VirtualTable.Add(function);
                    }
                    else
                    {
                        // Find slot in base types
                        var baseType = @class.BaseType;
                        Function matchedMethod = null;
                        while (baseType != null)
                        {
                            matchedMethod = CecilExtensions.TryMatchMethod(baseType, methodReference);
                            if (matchedMethod != null)
                                break;
                            baseType = baseType.BaseType;
                        }

                        if (matchedMethod == null)
                            throw new InvalidOperationException(string.Format("Could not find a slot for virtual function {0} in parents of class {1}", method, @class.Type.TypeReference));

                        function.VirtualSlot = matchedMethod.VirtualSlot;
                        @class.VirtualTable[function.VirtualSlot] = function;
                    }
                }
            }
        }

        private static MethodReference ResolveGenericMethod(TypeReference typeReference, MethodReference method)
        {
            var genericInstanceType = typeReference as GenericInstanceType;
            if (genericInstanceType != null)
                return method.MakeGeneric(genericInstanceType.GenericArguments.ToArray());

            return method;
        }

        class MemberEqualityComparer : IEqualityComparer<MemberReference>
        {
            public static readonly MemberEqualityComparer Default = new MemberEqualityComparer();

            public bool Equals(MemberReference x, MemberReference y)
            {
                return x.FullName == y.FullName;
            }

            public int GetHashCode(MemberReference obj)
            {
                return obj.FullName.GetHashCode();
            }
        }
    }
}