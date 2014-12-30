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
#include <llvm/Support/raw_ostream.h>

using namespace llvm;

template<typename DIT>
DIT unwrapDI(LLVMDIDescriptor ref) {
	return DIT(ref);
}

inline LLVMDIDescriptor wrap(const DIDescriptor& Vals) {
	return reinterpret_cast<MDNode*>(Vals.get());
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

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateFile(
	LLVMDIBuilderRef Builder,
	const char* Filename,
	const char* Directory) {
	return wrap(Builder->createFile(Filename, Directory));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateSubroutineType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor File,
	LLVMDIDescriptor ParameterTypes) {
	return wrap(Builder->createSubroutineType(
		unwrapDI<DIFile>(File),
		unwrapDI<DITypeArray>(ParameterTypes)));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateFunction(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	const char* LinkageName,
	LLVMDIDescriptor File,
	unsigned LineNo,
	LLVMDIDescriptor Ty,
	bool isLocalToUnit,
	bool isDefinition,
	unsigned ScopeLine,
	unsigned Flags,
	bool isOptimized,
	LLVMValueRef Fn,
	LLVMDIDescriptor TParam,
	LLVMDIDescriptor Decl) {
	return wrap(Builder->createFunction(
		unwrapDI<DIScope>(Scope), Name, LinkageName,
		unwrapDI<DIFile>(File), LineNo,
		unwrapDI<DICompositeType>(Ty), isLocalToUnit, isDefinition, ScopeLine,
		Flags, isOptimized,
		unwrap<Function>(Fn),
		unwrapDI<MDNode*>(TParam),
		unwrapDI<MDNode*>(Decl)));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateBasicType(
	LLVMDIBuilderRef Builder,
	const char* Name,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Encoding) {
	return wrap(Builder->createBasicType(
		Name, SizeInBits,
		AlignInBits, Encoding));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreatePointerType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor PointeeTy,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	const char* Name) {
	return wrap(Builder->createPointerType(
		unwrapDI<DIType>(PointeeTy), SizeInBits, AlignInBits, Name));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateForwardDecl(
	LLVMDIBuilderRef Builder,
	unsigned Tag,
	const char* Name,
	LLVMDIDescriptor Scope,
	LLVMDIDescriptor File,
	unsigned Line,
	unsigned RuntimeLang,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	const char* UniqueId) {
	return wrap(Builder->createForwardDecl(
		Tag,
		Name,
		unwrapDI<DIDescriptor>(Scope),
		unwrapDI<DIFile>(File),
		Line,
		RuntimeLang,
		SizeInBits,
		AlignInBits,
		UniqueId
		));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateStructType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Flags,
	LLVMDIDescriptor DerivedFrom,
	LLVMDIDescriptor Elements,
	unsigned RunTimeLang,
	LLVMDIDescriptor VTableHolder,
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

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateClassType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	uint64_t OffsetInBits,
	unsigned Flags,
	LLVMDIDescriptor DerivedFrom,
	LLVMDIDescriptor Elements,
	LLVMDIDescriptor VTableHolder,
	LLVMDIDescriptor TemplateParms,
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

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateMemberType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNo,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	uint64_t OffsetInBits,
	unsigned Flags,
	LLVMDIDescriptor Ty) {
	return wrap(Builder->createMemberType(
		unwrapDI<DIDescriptor>(Scope), Name,
		unwrapDI<DIFile>(File), LineNo,
		SizeInBits, AlignInBits, OffsetInBits, Flags,
		unwrapDI<DIType>(Ty)));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateLexicalBlock(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	LLVMDIDescriptor File,
	unsigned Line,
	unsigned Col,
	unsigned Discriminator) {
	return wrap(Builder->createLexicalBlock(
		unwrapDI<DIDescriptor>(Scope),
		unwrapDI<DIFile>(File), Line, Col
		));
}
extern "C" LLVMDIDescriptor LLVMDIBuilderCreateExpression(LLVMDIBuilderRef Builder, int64_t* Addresses, unsigned NumAddresses)
{
	return wrap(Builder->createExpression(ArrayRef<int64_t>(Addresses, NumAddresses)));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateGlobalVariable(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Context,
	const char* Name,
	const char* LinkageName,
	LLVMDIDescriptor File,
	unsigned LineNo,
	LLVMDIDescriptor Ty,
	bool isLocalToUnit,
	LLVMValueRef Val,
	LLVMDIDescriptor Decl) {
	return wrap(Builder->createGlobalVariable(unwrapDI<DIDescriptor>(Context),
		Name,
		LinkageName,
		unwrapDI<DIFile>(File),
		LineNo,
		unwrapDI<DIType>(Ty),
		isLocalToUnit,
		unwrap<Constant>(Val),
		unwrapDI<MDNode*>(Decl)));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateLocalVariable(
	LLVMDIBuilderRef Builder,
	unsigned Tag,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNo,
	LLVMDIDescriptor Ty,
	bool AlwaysPreserve,
	unsigned Flags,
	unsigned ArgNo) {
	return wrap(Builder->createLocalVariable(Tag,
		unwrapDI<DIDescriptor>(Scope), Name,
		unwrapDI<DIFile>(File),
		LineNo,
		unwrapDI<DIType>(Ty), AlwaysPreserve, Flags, ArgNo));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateArrayType(
	LLVMDIBuilderRef Builder,
	uint64_t Size,
	uint64_t AlignInBits,
	LLVMDIDescriptor Ty,
	LLVMDIDescriptor Subscripts) {
	return wrap(Builder->createArrayType(Size, AlignInBits,
		unwrapDI<DIType>(Ty),
		unwrapDI<DIArray>(Subscripts)));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateVectorType(
	LLVMDIBuilderRef Builder,
	uint64_t Size,
	uint64_t AlignInBits,
	LLVMDIDescriptor Ty,
	LLVMDIDescriptor Subscripts) {
	return wrap(Builder->createVectorType(Size, AlignInBits,
		unwrapDI<DIType>(Ty),
		unwrapDI<DIArray>(Subscripts)));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderGetOrCreateSubrange(
	LLVMDIBuilderRef Builder,
	int64_t Lo,
	int64_t Count) {
	return wrap(Builder->getOrCreateSubrange(Lo, Count));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderGetOrCreateArray(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor* Ptr,
	unsigned Count) {
	return wrap(Builder->getOrCreateArray(
		ArrayRef<Metadata*>(reinterpret_cast<Metadata**>(Ptr), Count)));
}

extern "C" LLVMValueRef LLVMDIBuilderInsertDeclareAtEnd(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Val,
	LLVMDIDescriptor VarInfo,
	LLVMDIDescriptor Expr,
	LLVMBasicBlockRef InsertAtEnd) {
	return wrap(Builder->insertDeclare(
		unwrap(Val),
		unwrapDI<DIVariable>(VarInfo),
		unwrapDI<DIExpression>(Expr),
		unwrap(InsertAtEnd)));
}

extern "C" LLVMValueRef LLVMDIBuilderInsertDeclareBefore(
	LLVMDIBuilderRef Builder,
	LLVMValueRef Val,
	LLVMDIDescriptor VarInfo,
	LLVMDIDescriptor Expr,
	LLVMValueRef InsertBefore) {
	return wrap(Builder->insertDeclare(
		unwrap(Val),
		unwrapDI<DIVariable>(VarInfo),
		unwrapDI<DIExpression>(Expr),
		unwrap<Instruction>(InsertBefore)));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateEnumerator(
	LLVMDIBuilderRef Builder,
	const char* Name,
	uint64_t Val)
{
	return wrap(Builder->createEnumerator(Name, Val));
}

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateEnumerationType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	LLVMDIDescriptor Elements,
	LLVMDIDescriptor ClassType)
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

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateUnionType(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNumber,
	uint64_t SizeInBits,
	uint64_t AlignInBits,
	unsigned Flags,
	LLVMDIDescriptor Elements,
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

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateTemplateTypeParameter(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor Ty,
	LLVMDIDescriptor File,
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

extern "C" LLVMDIDescriptor LLVMDIBuilderCreateNameSpace(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor Scope,
	const char* Name,
	LLVMDIDescriptor File,
	unsigned LineNo)
{
	return wrap(Builder->createNameSpace(
		unwrapDI<DIDescriptor>(Scope),
		Name,
		unwrapDI<DIFile>(File),
		LineNo));
}

extern "C" void LLVMDICompositeTypeSetTypeArray(
	LLVMDIBuilderRef Builder,
	LLVMDIDescriptor* CompositeType,
	LLVMDIDescriptor TypeArray)
{
	auto compositeType = unwrapDI<DICompositeType>(*CompositeType);
	Builder->replaceArrays(compositeType, unwrapDI<DIArray>(TypeArray));
	*CompositeType = wrap(compositeType);
}

extern "C" void LLVMAddModuleFlag(LLVMModuleRef M,
	const char *name,
	uint32_t value) {
	unwrap(M)->addModuleFlag(Module::Warning, name, value);
}

extern "C" LLVMValueRef LLVMDIMetadataAsValue(LLVMDIDescriptor Value)
{
	return wrap(MetadataAsValue::get(Value->getContext(), Value));
}

extern "C" char* LLVMDIPrintDescriptorToString(LLVMDIDescriptor Value)
{
	std::string buf;
	raw_string_ostream os(buf);

	if (unwrapDI<DIDescriptor>(Value))
		unwrapDI<DIDescriptor>(Value)->print(os);
	else
		os << "Printing <null> Value";

	os.flush();

	return strdup(buf.c_str());
}

extern "C" LLVMDIDescriptor LLVMDICreateDebugLocation(unsigned Line, unsigned Col, LLVMDIDescriptor Scope, LLVMDIDescriptor InlinedAt)
{
	return DebugLoc::get(Line, Col, Scope, InlinedAt).getAsMDNode();
}

extern "C" uint32_t LLVMDIGetDebugMetadataVersion()
{
	return DEBUG_METADATA_VERSION;
}
