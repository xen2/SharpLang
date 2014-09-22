// Copyright 2013 The Rust Project Developers. See the COPYRIGHT
// file at the top-level directory of this distribution and at
// http://rust-lang.org/COPYRIGHT.
//
// Licensed under the Apache License, Version 2.0 <LICENSE-APACHE or
// http://www.apache.org/licenses/LICENSE-2.0> or the MIT license
// <LICENSE-MIT or http://opensource.org/licenses/MIT>, at your
// option. This file may not be copied, modified, or distributed
// except according to those terms.

#include "DebugInfo.h"

#include <llvm/IR/Module.h>

using namespace llvm;

template<typename DIT>
DIT unwrapDI(LLVMValueRef ref) {
	return DIT(ref ? unwrap<MDNode>(ref) : NULL);
}

extern "C" LLVMDIBuilderRef LLVMDIBuilderCreate(LLVMModuleRef M) {
	return new DIBuilder(*unwrap(M));
}

extern "C" void LLVMDIBuilderDispose(LLVMDIBuilderRef Builder) {
	delete Builder;
}

extern "C" void LLVMDIBuilderFinalize(LLVMDIBuilderRef Builder) {
	Builder->finalize();
}

extern "C" void LLVMDIBuilderCreateCompileUnit(
	LLVMDIBuilderRef Builder,
	unsigned Lang,
	const char* File,
	const char* Dir,
	const char* Producer,
	bool isOptimized,
	const char* Flags,
	unsigned RuntimeVer,
	const char* SplitName) {
	Builder->createCompileUnit(Lang, File, Dir, Producer, isOptimized,
		Flags, RuntimeVer, SplitName);
}

extern "C" LLVMValueRef LLVMDIBuilderCreateFile(
	LLVMDIBuilderRef Builder,
	const char* Filename,
	const char* Directory) {
	return wrap(Builder->createFile(Filename, Directory));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateSubroutineType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef File,
	LLVMValueRef ParameterTypes) {
	return wrap(Builder->createSubroutineType(
		unwrapDI<DIFile>(File),
		unwrapDI<DITypeArray>(ParameterTypes)));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateFunction(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	const char* LinkageName,
	LLVMValueRef File,
	unsigned LineNo,
	LLVMValueRef Ty,
	bool isLocalToUnit,
	bool isDefinition,
	unsigned ScopeLine,
	unsigned Flags,
	bool isOptimized,
	LLVMValueRef Fn,
	LLVMValueRef TParam,
	LLVMValueRef Decl) {
	return wrap(Builder->createFunction(
		unwrapDI<DIScope>(Scope), Name, LinkageName,
		unwrapDI<DIFile>(File), LineNo,
		unwrapDI<DICompositeType>(Ty), isLocalToUnit, isDefinition, ScopeLine,
		Flags, isOptimized,
		unwrap<Function>(Fn),
		unwrapDI<MDNode*>(TParam),
		unwrapDI<MDNode*>(Decl)));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateBasicType(
	LLVMDIBuilderRef Builder,
	const char* Name,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Encoding) {
	return wrap(Builder->createBasicType(
		Name, SizeInBits,
		AlignInBits, Encoding));
}

extern "C" LLVMValueRef LLVMDIBuilderCreatePointerType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef PointeeTy,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	const char* Name) {
	return wrap(Builder->createPointerType(
		unwrapDI<DIType>(PointeeTy), SizeInBits, AlignInBits, Name));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateStructType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Flags,
	LLVMValueRef DerivedFrom,
	LLVMValueRef Elements,
	unsigned RunTimeLang,
	LLVMValueRef VTableHolder,
	const char *UniqueId) {
	return wrap(Builder->createStructType(
		unwrapDI<DIDescriptor>(Scope),
		Name,
		unwrapDI<DIFile>(File),
		LineNumber,
		SizeInBits,
		AlignInBits,
		Flags,
		unwrapDI<DIType>(DerivedFrom),
		unwrapDI<DIArray>(Elements),
		RunTimeLang,
		unwrapDI<DIType>(VTableHolder),
		UniqueId
		));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateClassType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	uint64_t OffsetInBits,
	unsigned Flags,
	LLVMValueRef DerivedFrom,
	LLVMValueRef Elements,
	LLVMValueRef VTableHolder,
	LLVMValueRef TemplateParms,
	const char *UniqueId) {
	return wrap(Builder->createClassType(
		unwrapDI<DIDescriptor>(Scope),
		Name,
		unwrapDI<DIFile>(File),
		LineNumber,
		SizeInBits,
		AlignInBits,
		OffsetInBits,
		Flags,
		unwrapDI<DIType>(DerivedFrom),
		unwrapDI<DIArray>(Elements),
		unwrapDI<DIType>(VTableHolder),
		unwrapDI<MDNode*>(TemplateParms),
		UniqueId
		));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateMemberType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNo,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	uint64_t OffsetInBits,
	unsigned Flags,
	LLVMValueRef Ty) {
	return wrap(Builder->createMemberType(
		unwrapDI<DIDescriptor>(Scope), Name,
		unwrapDI<DIFile>(File), LineNo,
		SizeInBits, AlignInBits, OffsetInBits, Flags,
		unwrapDI<DIType>(Ty)));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateLexicalBlock(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	LLVMValueRef File,
	unsigned Line,
	unsigned Col,
	unsigned Discriminator) {
	return wrap(Builder->createLexicalBlock(
		unwrapDI<DIDescriptor>(Scope),
		unwrapDI<DIFile>(File), Line, Col
		));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateStaticVariable(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Context,
	const char* Name,
	const char* LinkageName,
	LLVMValueRef File,
	unsigned LineNo,
	LLVMValueRef Ty,
	bool isLocalToUnit,
	LLVMValueRef Val,
	LLVMValueRef Decl) {
	return wrap(Builder->createStaticVariable(unwrapDI<DIDescriptor>(Context),
		Name,
		LinkageName,
		unwrapDI<DIFile>(File),
		LineNo,
		unwrapDI<DIType>(Ty),
		isLocalToUnit,
		unwrap(Val),
		unwrapDI<MDNode*>(Decl)));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateLocalVariable(
	LLVMDIBuilderRef Builder,
	unsigned Tag,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNo,
	LLVMValueRef Ty,
	bool AlwaysPreserve,
	unsigned Flags,
	unsigned ArgNo) {
	return wrap(Builder->createLocalVariable(Tag,
		unwrapDI<DIDescriptor>(Scope), Name,
		unwrapDI<DIFile>(File),
		LineNo,
		unwrapDI<DIType>(Ty), AlwaysPreserve, Flags, ArgNo));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateArrayType(
	LLVMDIBuilderRef Builder,
	uint64_t Size,
	uint64_t AlignInBits,
	LLVMValueRef Ty,
	LLVMValueRef Subscripts) {
	return wrap(Builder->createArrayType(Size, AlignInBits,
		unwrapDI<DIType>(Ty),
		unwrapDI<DIArray>(Subscripts)));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateVectorType(
	LLVMDIBuilderRef Builder,
	uint64_t Size,
	uint64_t AlignInBits,
	LLVMValueRef Ty,
	LLVMValueRef Subscripts) {
	return wrap(Builder->createVectorType(Size, AlignInBits,
		unwrapDI<DIType>(Ty),
		unwrapDI<DIArray>(Subscripts)));
}

extern "C" LLVMValueRef LLVMDIBuilderGetOrCreateSubrange(
	LLVMDIBuilderRef Builder,
	int64_t Lo,
	int64_t Count) {
	return wrap(Builder->getOrCreateSubrange(Lo, Count));
}

extern "C" LLVMValueRef LLVMDIBuilderGetOrCreateArray(
	LLVMDIBuilderRef Builder,
	LLVMValueRef* Ptr,
	unsigned Count) {
	return wrap(Builder->getOrCreateArray(
		ArrayRef<Value*>(reinterpret_cast<Value**>(Ptr), Count)));
}

extern "C" LLVMValueRef LLVMDIBuilderInsertDeclareAtEnd(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Val,
	LLVMValueRef VarInfo,
	LLVMBasicBlockRef InsertAtEnd) {
	return wrap(Builder->insertDeclare(
		unwrap(Val),
		unwrapDI<DIVariable>(VarInfo),
		unwrap(InsertAtEnd)));
}

extern "C" LLVMValueRef LLVMDIBuilderInsertDeclareBefore(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Val,
	LLVMValueRef VarInfo,
	LLVMValueRef InsertBefore) {
	return wrap(Builder->insertDeclare(
		unwrap(Val),
		unwrapDI<DIVariable>(VarInfo),
		unwrap<Instruction>(InsertBefore)));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateEnumerator(
	LLVMDIBuilderRef Builder,
	const char* Name,
	uint64_t Val)
{
	return wrap(Builder->createEnumerator(Name, Val));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateEnumerationType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	LLVMValueRef Elements,
	LLVMValueRef ClassType)
{
	return wrap(Builder->createEnumerationType(
		unwrapDI<DIDescriptor>(Scope),
		Name,
		unwrapDI<DIFile>(File),
		LineNumber,
		SizeInBits,
		AlignInBits,
		unwrapDI<DIArray>(Elements),
		unwrapDI<DIType>(ClassType)));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateUnionType(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Flags,
	LLVMValueRef Elements,
	unsigned RunTimeLang,
	const char* UniqueId)
{
	return wrap(Builder->createUnionType(
		unwrapDI<DIDescriptor>(Scope),
		Name,
		unwrapDI<DIFile>(File),
		LineNumber,
		SizeInBits,
		AlignInBits,
		Flags,
		unwrapDI<DIArray>(Elements),
		RunTimeLang,
		UniqueId
		));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateTemplateTypeParameter(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef Ty,
	LLVMValueRef File,
	unsigned LineNo,
	unsigned ColumnNo)
{
	return wrap(Builder->createTemplateTypeParameter(
		unwrapDI<DIDescriptor>(Scope),
		Name,
		unwrapDI<DIType>(Ty),
		unwrapDI<MDNode*>(File),
		LineNo,
		ColumnNo));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateOpDeref(LLVMTypeRef IntTy)
{
	return LLVMConstInt(IntTy, DIBuilder::OpDeref, true);
}

extern "C" LLVMValueRef LLVMDIBuilderCreateOpPlus(LLVMTypeRef IntTy)
{
	return LLVMConstInt(IntTy, DIBuilder::OpPlus, true);
}

extern "C" LLVMValueRef LLVMDIBuilderCreateComplexVariable(
	LLVMDIBuilderRef Builder,
	unsigned Tag,
	LLVMValueRef Scope,
	const char *Name,
	LLVMValueRef File,
	unsigned LineNo,
	LLVMValueRef Ty,
	LLVMValueRef* AddrOps,
	unsigned AddrOpsCount,
	unsigned ArgNo)
{
	llvm::ArrayRef<llvm::Value*> addr_ops((llvm::Value**)AddrOps, AddrOpsCount);

	return wrap(Builder->createComplexVariable(
		Tag,
		unwrapDI<DIDescriptor>(Scope),
		Name,
		unwrapDI<DIFile>(File),
		LineNo,
		unwrapDI<DIType>(Ty),
		addr_ops,
		ArgNo
		));
}

extern "C" LLVMValueRef LLVMDIBuilderCreateNameSpace(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Scope,
	const char* Name,
	LLVMValueRef File,
	unsigned LineNo)
{
	return wrap(Builder->createNameSpace(
		unwrapDI<DIDescriptor>(Scope),
		Name,
		unwrapDI<DIFile>(File),
		LineNo));
}

extern "C" void LLVMDICompositeTypeSetTypeArray(
	LLVMValueRef CompositeType,
	LLVMValueRef TypeArray)
{
	unwrapDI<DICompositeType>(CompositeType).setArrays(unwrapDI<DIArray>(TypeArray));
}

extern "C" void LLVMAddModuleFlag(LLVMModuleRef M,
	const char *name,
	uint32_t value) {
	unwrap(M)->addModuleFlag(Module::Warning, name, value);
}