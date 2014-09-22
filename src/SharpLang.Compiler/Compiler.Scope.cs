using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;
using SharpLLVM;

namespace SharpLang.CompilerServices
{
    public partial class Compiler
    {
        private Dictionary<string, ValueRef> debugNamespaces = new Dictionary<string, ValueRef>();
        private Dictionary<Class, ValueRef> debugClasses = new Dictionary<Class, ValueRef>();

        private ValueRef GetOrCreateDebugNamespace(string @namespace)
        {
            if (@namespace == string.Empty)
                return ValueRef.Empty;

            // Already done before?
            ValueRef debugNamespace;
            if (debugNamespaces.TryGetValue(@namespace, out debugNamespace))
                return debugNamespace;

            var debugParentNamespace = ValueRef.Empty;

            // Split between parent and current node (last element of the path)
            var splitIndex = @namespace.LastIndexOf('.');
            if (splitIndex != -1)
            {
                // Resolve parent namespace (recursively)
                var parentNamespace = @namespace.Substring(0, splitIndex);
                debugParentNamespace = GetOrCreateDebugNamespace(parentNamespace);
            }

            // Create debug namespace for this node
            debugNamespace = LLVM.DIBuilderCreateNameSpace(debugBuilder, debugParentNamespace, @namespace.Substring(splitIndex + 1), ValueRef.Empty, 0);

            // Register
            debugNamespaces.Add(@namespace, debugNamespace);

            return debugNamespace;
        }

        private void PrepareScopes(FunctionCompilerContext functionContext)
        {
            var function = functionContext.Function;
            var methodReference = function.MethodReference;
            var body = functionContext.Body;

            // Add root scope variables
            // Note: could be null
            var newScope = new Scope(body.Scope);
            functionContext.Scopes.Add(newScope);

            // Find class scope
            var debugClass = GetOrCreateDebugClass(GetClass(function.DeclaringType));

            // Update debug information
            int line = 0;

            var startSequencePoint = body.Instructions[0].SequencePoint;
            string url;
            if (startSequencePoint != null)
            {
                url = startSequencePoint.Document.Url;
                line = startSequencePoint.StartLine;
            }
            else
            {
                url = assembly.MainModule.FullyQualifiedName;
            }

            functionContext.DebugFile = LLVM.DIBuilderCreateFile(debugBuilder, Path.GetFileName(url), Path.GetDirectoryName(url));
            var functionParameterTypes = LLVM.DIBuilderGetOrCreateArray(debugBuilder, new ValueRef[0]);
            var functionType = LLVM.DIBuilderCreateSubroutineType(debugBuilder, functionContext.DebugFile, functionParameterTypes);

            // Replace . with :: so that gdb properly understands it is namespaces.
            var methodDebugName = string.Format("{0}::{1}", methodReference.DeclaringType.FullName.Replace(".", "::"), methodReference.Name);

            newScope.GeneratedScope = LLVM.DIBuilderCreateFunction(debugBuilder, debugClass, methodReference.Name, methodDebugName,
                functionContext.DebugFile, (uint)line, functionType,
                false, true, (uint)line, 0, false, function.GeneratedValue, ValueRef.Empty, ValueRef.Empty);

            SetupDebugLocation(body.Instructions[0].SequencePoint, newScope);
            if (body.Scope != null)
            {
                EnterScope(functionContext, newScope);
            }
            else
            {
                // Emit locals (if no scopes)
                for (int index = 0; index < body.Variables.Count; index++)
                {
                    var variable = functionContext.Locals[index];
                    var variableName = body.Variables[index].Name;
                    if (string.IsNullOrEmpty(variableName))
                        variableName = "var" + index;

                    EmitDebugVariable(functionContext, body.Instructions[0].SequencePoint, newScope.GeneratedScope, variable, DW_TAG.auto_variable, variableName);
                }
            }

            // Emit args
            for (int index = 0; index < function.ParameterTypes.Length; index++)
            {
                var arg = functionContext.Arguments[index];

                var argName = LLVM.GetValueName(arg.Value);

                EmitDebugVariable(functionContext, body.Instructions[0].SequencePoint, newScope.GeneratedScope, arg, DW_TAG.arg_variable, argName, index + 1);
            }
        }

        private ValueRef GetOrCreateDebugClass(Class @class)
        {
            ValueRef debugClass;
            if (debugClasses.TryGetValue(@class, out debugClass))
                return debugClass;

            // Find namespace scope
            var debugNamespace = GetOrCreateDebugNamespace(@class.Type.TypeReference.Namespace);

            var parentClass = @class.BaseType;
            var parentDebugClass = parentClass != null ? GetOrCreateDebugClass(parentClass) : ValueRef.Empty;

            // Create debug version of the class
            var size = LLVM.ABISizeOfType(targetData, @class.Type.DefaultType) * 8;
            var align = LLVM.ABIAlignmentOfType(targetData, @class.Type.DefaultType) * 8;
            var emptyArray = LLVM.DIBuilderGetOrCreateArray(debugBuilder, new ValueRef[0]);

            debugClass = LLVM.DIBuilderCreateClassType(debugBuilder, debugNamespace, @class.Type.TypeReference.Name, ValueRef.Empty, 0, size, align, 0, 0, parentDebugClass, emptyArray, ValueRef.Empty, ValueRef.Empty, @class.Type.TypeReference.FullName);
            
            debugClasses.Add(@class, debugClass);
            return debugClass;
        }

        private void ProcessScopes(FunctionCompilerContext functionContext, Instruction instruction)
        {
            var scopes = functionContext.Scopes;

            // Exit finished scopes
            for (int index = scopes.Count - 1; index >= 0; index--)
            {
                var scope = scopes[index];
                if (scope.Source != null && instruction.Offset > scope.Source.End.Offset)
                    scopes.RemoveAt(index);
                else
                    break;
            }

            var lastScope = scopes[scopes.Count - 1];
            bool foundNewScope = true;
            while (foundNewScope)
            {
                foundNewScope = false;
                if (lastScope.Source != null && lastScope.Source.HasScopes)
                {
                    foreach (var childScope in lastScope.Source.Scopes)
                    {
                        if (instruction == childScope.Start)
                        {
                            lastScope = CreateScope(functionContext, lastScope, childScope);
                            scopes.Add(lastScope);

                            EnterScope(functionContext, lastScope);

                            foundNewScope = true;
                            break;
                        }
                    }
                }
            }

            if (instruction.SequencePoint != null)
                SetupDebugLocation(instruction.SequencePoint, lastScope);
        }

        private void SetupDebugLocation(SequencePoint sequencePoint, Scope lastScope)
        {
            var line = sequencePoint != null ? sequencePoint.StartLine : 0;
            var column = sequencePoint != null ? sequencePoint.StartColumn : 0;
            var debugLoc = LLVM.MDNodeInContext(context,
                new[]
                {
                    LLVM.ConstInt(int32Type, (ulong) line, true), LLVM.ConstInt(int32Type, (ulong) column, true),
                    lastScope.GeneratedScope, ValueRef.Empty
                });
            LLVM.SetCurrentDebugLocation(builder, debugLoc);
        }

        private Scope CreateScope(FunctionCompilerContext functionContext, Scope parentScope, Mono.Cecil.Cil.Scope cecilScope)
        {
            var newScope = new Scope(cecilScope);
            var sequencePoint = newScope.Source.Start.SequencePoint;
            if (sequencePoint != null)
            {
                newScope.GeneratedScope = LLVM.DIBuilderCreateLexicalBlock(debugBuilder, parentScope.GeneratedScope,
                    functionContext.DebugFile,
                    (uint) sequencePoint.StartLine, (uint) sequencePoint.StartColumn, 0);
            }

            return newScope;
        }

        private void EnterScope(FunctionCompilerContext functionContext, Scope newScope)
        {
            if (newScope.Source != null)
            {
                SetupDebugLocation(newScope.Source.Start.SequencePoint, newScope);
                if (newScope.Source.HasVariables)
                {
                    foreach (var local in newScope.Source.Variables)
                    {
                        var variable = functionContext.Locals[local.Index];
                        var variableName = local.Name;

                        EmitDebugVariable(functionContext, newScope.Source.Start.SequencePoint, newScope.GeneratedScope, variable, DW_TAG.auto_variable, variableName);
                    }
                }
            }
        }

        private void EmitDebugVariable(FunctionCompilerContext functionContext, SequencePoint sequencePoint, ValueRef generatedScope, StackValue variable, DW_TAG dwarfType, string variableName, int argIndex = 0)
        {
            var debugType = CreateDebugType(functionContext, variable.Type);

            // TODO: Detect where variable is actually declared (first use of local?)
            var debugLocalVariable = LLVM.DIBuilderCreateLocalVariable(debugBuilder,
                (uint)dwarfType,
                generatedScope, variableName, functionContext.DebugFile, sequencePoint != null ? (uint)sequencePoint.StartLine : 0, debugType,
                true, 0, (uint)argIndex);

            var debugVariableDeclare = LLVM.DIBuilderInsertDeclareAtEnd(debugBuilder, variable.Value, debugLocalVariable,
                LLVM.GetInsertBlock(builder));
            LLVM.SetInstDebugLocation(builder, debugVariableDeclare);
        }

        public enum DW_TAG
        {
            auto_variable = 0x100,
            arg_variable = 0x101,
        }

        public enum DW_ATE
        {
            Boolean = 0x02,
            Float = 0x04,
            Signed = 0x05,
            Unsigned = 0x07,
        }

        private ValueRef CreateDebugType(FunctionCompilerContext functionContext, Type type)
        {
            ulong size = 0;
            ulong align = 0;

            switch (type.TypeReference.MetadataType)
            {
                case MetadataType.Boolean:
                case MetadataType.SByte:
                case MetadataType.Byte:
                case MetadataType.Int16:
                case MetadataType.UInt16:
                case MetadataType.Int32:
                case MetadataType.UInt32:
                case MetadataType.Int64:
                case MetadataType.UInt64:
                case MetadataType.Single:
                case MetadataType.Double:
                case MetadataType.Char:
                case MetadataType.IntPtr:
                case MetadataType.UIntPtr:
                case MetadataType.Pointer:
                    size = LLVM.ABISizeOfType(targetData, type.DefaultType) * 8;
                    align = LLVM.ABIAlignmentOfType(targetData, type.DefaultType) * 8;
                    break;
                default:
                    break;
            }

            switch (type.TypeReference.MetadataType)
            {
                case MetadataType.Boolean:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "bool", size, align, (uint)DW_ATE.Boolean);
                case MetadataType.SByte:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "sbyte", size, align, (uint)DW_ATE.Signed);
                case MetadataType.Byte:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "byte", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Int16:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "short", size, align, (uint)DW_ATE.Signed);
                case MetadataType.UInt16:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "ushort", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Int32:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "int", size, align, (uint)DW_ATE.Signed);
                case MetadataType.UInt32:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "uint", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Int64:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "long", size, align, (uint)DW_ATE.Signed);
                case MetadataType.UInt64:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "ulong", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Single:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "float", size, align, (uint)DW_ATE.Float);
                case MetadataType.Double:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "double", size, align, (uint)DW_ATE.Float);
                case MetadataType.Char:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "char", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.IntPtr:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "IntPtr", size, align, (uint)DW_ATE.Signed);
                case MetadataType.UIntPtr:
                    return LLVM.DIBuilderCreateBasicType(debugBuilder, "UIntPtr", size, align, (uint)DW_ATE.Unsigned);
                case MetadataType.Pointer:
                    var elementType = GetType(((PointerType)type.TypeReference).ElementType);
                    return LLVM.DIBuilderCreatePointerType(debugBuilder, CreateDebugType(functionContext, elementType), size, align, type.TypeReference.Name);
                default:
                    // For now, let's have a fallback since lot of types are not supported yet.
                    return CreateDebugType(functionContext, intPtr);
            }
        }
    }
}
