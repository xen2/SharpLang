using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Provides access to C# LLVM code generator.
    /// </summary>
    public sealed partial class Compiler
    {
        /// <summary> Current module being generated. </summary>
        private ModuleRef module;
        private ContextRef context;

        // <summary> Builder used for method codegen. <summary>
        private BuilderRef builder;
        // <summary> Extra builder used for method codegen. <summary>
        private BuilderRef builder2;

        private DIBuilderRef debugBuilder;
        private TargetDataRef targetData;

        /// <summary> Currently compiled assembly. </summary>
        private AssemblyDefinition assembly;
        
        /// <summary> Corlib assembly. </summary>
        private AssemblyDefinition corlib;

        /// <summary> Cecil TypeReference to generated SharpLang Type mapping. </summary>
        private Dictionary<TypeReference, Type> types = new Dictionary<TypeReference, Type>(MemberEqualityComparer.Default);

        /// <summary> Cecil methodReference to generated SharpLang Function mapping. </summary>
        private Dictionary<MethodReference, Function> functions = new Dictionary<MethodReference, Function>(MemberEqualityComparer.Default);

        /// <summary> List of classes that still need to be generated. </summary>
        private Queue<Type> classesToGenerate = new Queue<Type>();

        /// <summary> List of methods that still need to be generated. </summary>
        private Queue<KeyValuePair<MethodReference, Function>> methodsToCompile = new Queue<KeyValuePair<MethodReference, Function>>();


        /// <summary> True when running unit tests. This will try to avoid using real mscorlib for faster codegen, linking and testing. </summary>
        public bool TestMode { get; set; }

        public void PrepareAssembly(AssemblyDefinition assembly)
        {
            this.assembly = assembly;

            RegisterExternalTypes();

            // Resolve corlib assembly
            corlib = assembly.MainModule.Import(typeof (void)).Resolve().Module.Assembly;

            // Prepare LLVM context, module and data layouts
            context = LLVM.GetGlobalContext();
            module = LLVM.ModuleCreateWithName(assembly.Name.Name);

            // TODO: Choose appropriate triple depending on target
            LLVM.SetTarget(module, "i686-pc-mingw32");

            // Prepare system types, for easier access
            InitializeCommonTypes();

            // Prepare LLVM builders
            builder = LLVM.CreateBuilderInContext(context);
            builder2 = LLVM.CreateBuilderInContext(context);
            debugBuilder = LLVM.DIBuilderCreate(module);
        }

        public void RegisterType(TypeReference typeReference)
        {
            var type = GetType(typeReference, TypeState.TypeComplete);
            EmitType(type, true);
        }

        public void ProcessAssembly(AssemblyDefinition assembly)
        {
            // Transfom all types in this assembly into SharpLang types.
            foreach (var assemblyModule in assembly.Modules)
            {
                var typeReferences = assemblyModule.GetTypeReferences();
                foreach (var type in typeReferences)
                {
                    GetType(type, TypeState.TypeComplete);
                }

                var memberReferences = assemblyModule.GetMemberReferences();
                foreach (var member in memberReferences)
                {
                    var method = member as MethodReference;
                    if (member.DeclaringType.ContainsGenericParameter)
                        continue;
                    GetType(member.DeclaringType, TypeState.TypeComplete);
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
            LLVM.DIBuilderCreateCompileUnit(debugBuilder,
                0x4, // DW_LANG_C_plus_plus
                "file", "directory", "SharpLang", false, string.Empty, 1, string.Empty);

            LLVM.AddModuleFlag(module, "Dwarf Version", 4);
            LLVM.AddModuleFlag(module, "Debug Info Version", 1);

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
                var mainFunctionType = LLVM.FunctionType(int32LLVM, new TypeRef[0], false);
	            var mainFunction = LLVM.AddFunction(module, "main", mainFunctionType);
                LLVM.SetLinkage(mainFunction, Linkage.ExternalLinkage);
                LLVM.PositionBuilderAtEnd(builder, LLVM.AppendBasicBlockInContext(context, mainFunction, string.Empty));

	            var parameters = (entryPoint.ParameterTypes.Length > 0)
	                ? new[] { LLVM.ConstPointerNull(entryPoint.ParameterTypes[0].DefaultTypeLLVM) }
	                : new ValueRef[0];

                LLVM.BuildCall(builder, entryPoint.GeneratedValue, parameters, string.Empty);
                LLVM.BuildRet(builder, LLVM.ConstInt(int32LLVM, 0, false));
	        }

            LLVM.DIBuilderFinalize(debugBuilder);
            LLVM.DIBuilderDispose(debugBuilder);
            LLVM.DisposeBuilder(builder);

            // Verify module
            string message;
            if (LLVM.VerifyModule(module, VerifierFailureAction.PrintMessageAction, out message))
            {
                throw new InvalidOperationException(message);
            }
            
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
            if (@class.Type.TypeReferenceCecil is ArrayType)
                return;

            var typeDefinition = GetMethodTypeDefinition(@class.Type.TypeReferenceCecil);

            bool isInterface = typeDefinition.IsInterface;

            // Process methods, Virtual first, then non virtual, then static
            foreach (var method in typeDefinition.Methods.OrderBy(x => x.IsVirtual ? 0 : (!x.IsStatic ? 1 : 2)))
            {
                var methodReference = ResolveGenericMethod(@class.Type.TypeReferenceCecil, method);

                // If a method contains generic parameters, skip it
                // Its closed instantiations (with generic arguments) is what needs to be generated.
                // (except interface methods)
                // Using ResolveGenericsVisitor.ContainsGenericParameters because Cecil one's doesn't seem to match what .NET Type does.
                // TODO: Might need a more robust generic resolver/analyzer system soon.
                if (ResolveGenericsVisitor.ContainsGenericParameters(methodReference))
                    continue;

                var function = CreateFunction(methodReference);

                @class.Functions.Add(function);

                if (method.IsSpecialName && method.Name == ".cctor")
                {
                    @class.StaticCtor = function;
                }

                if (method.IsVirtual)
                {
                    if (isInterface)
                    {
                        // Store IMT slot, and unique IMT key (generated using global pointer)
                        function.VirtualSlot = (int)(GetMethodId(methodReference) % InterfaceMethodTableSize);
                        function.InterfaceSlot = function.GeneratedValue;
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
                            throw new InvalidOperationException(string.Format("Could not find a slot for virtual function {0} in parents of class {1}", method, @class.Type.TypeReferenceCecil));

                        function.VirtualSlot = matchedMethod.VirtualSlot;
                        @class.VirtualTable[function.VirtualSlot] = function;
                    }
                }
                else
                {
                    // New slot
                    function.VirtualSlot = @class.VirtualTable.Count;
                    @class.VirtualTable.Add(function);
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
    }
}