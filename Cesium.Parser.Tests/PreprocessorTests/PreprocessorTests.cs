using System.Diagnostics.CodeAnalysis;
using Cesium.Core;
using Cesium.Preprocessor;
using Cesium.TestFramework;
using Yoakke.SynKit.Lexer;

namespace Cesium.Parser.Tests.PreprocessorTests;

public class PreprocessorTests : VerifyTestBase
{
    private static async Task DoTest(
        [StringSyntax("cpp")] string source,
        Dictionary<string, string>? standardHeaders = null,
        Dictionary<string, IList<IToken<CPreprocessorTokenType>>>? defines = null)
    {
        string result = await DoPreprocess(source, standardHeaders, defines);
        await Verify(result, GetSettings());
    }

    private static async Task<string> DoPreprocess(string source, Dictionary<string, string>? standardHeaders = null, Dictionary<string, IList<IToken<CPreprocessorTokenType>>>? defines = null)
    {
        var filePath = "c:\\a\\b\\c.c";
        var lexer = new CPreprocessorLexer(filePath, source);
        var includeContext = new IncludeContextMock(standardHeaders ?? new Dictionary<string, string>());
        var definesContext = new InMemoryDefinesContext(defines ?? new Dictionary<string, IList<IToken<CPreprocessorTokenType>>>());
        var preprocessor = new CPreprocessor(filePath, lexer, includeContext, definesContext);
        var result = await preprocessor.ProcessSource();
        return result;
    }

    [Fact]
    public Task IdentityTest() => DoTest(@"int main(void)
{
    return 2 + 2;
}");

    [Fact]
    public Task Include() => DoTest(@"#include <foo.h>
int test()
{
    #include <bar.h>
}", new() { ["foo.h"] = "void foo() {}", ["bar.h"] = "int bar = 0;" });

    [Fact]
    public Task Include2() => DoTest(@"#include <foo.h>
#include <bar.h>
int test()
{
}", new() { ["foo.h"] = "void foo() {}", ["bar.h"] = "int bar = 0;" });

    [Fact]
    public Task IncludeTrailingWhitespacesIgnored() => DoTest($@"#include <foo.h>{"\t\t\t" /*Make whitespaces visible here */}
#include <bar.h>
int test()
{{
}}", new() { ["foo.h"] = "void foo() {}", ["bar.h"] = "int bar = 0;" });

    [Fact]
    public Task IncludeTrailingCommentsAreAllowed() => DoTest(
        "#include <foo.h> // comment",
        new() { ["foo.h"] = "int x = 0;" });

    [Fact]
    public Task IncludeNoWhitespaces() => DoTest(@"#include<foo.h>
int test()
{
    foo();
}", new() { ["foo.h"] = "void foo() {}" });

    [Fact]
    public Task NestedIncludes() => DoTest(@"#include <foo.h>

int test()
{
}", new() { ["foo.h"] = "#include <bar.h>", ["bar.h"] = "int bar = 0;" });

    [Fact]
    public Task NestedIncludes2() => DoTest(@"#include <foo.h>

int test()
{
}", new() { ["foo.h"] = "#include <bar.h>", ["bar.h"] = "#include <baz.h>", ["baz.h"] = "int bar = 0;" });

    [Fact]
    public Task IgnoredInclude() => DoTest("""
#ifdef MY_INCLUDE
#include MY_INCLUDE
#endif
#ifndef MY_INCLUDE
test
#endif
""");

    [Fact]
    public Task IncludeInElif() => DoTest("""
#define NUM 1
#if NUM == 2
#include NUM
#elif NUM == 1
#include <foo.h>
#else
#include NUM
#endif
""", new() { ["foo.h"] = "int x = 0;" });

    [Fact]
    public Task PragmaOnce() => DoTest(@"
int test()
{
#include <foo.h>
#include <foo.h>
}", new() { ["foo.h"] = "#pragma once\nprintfn();" });

    [Fact, NoVerify]
    public async Task ErrorMsg()
    {
        PreprocessorException err = await Assert.ThrowsAsync<PreprocessorException>(async () => await DoTest(
@"#error ""Error message"" test
int test()
{}"
        ));
        Assert.Equal(@"Error: ""Error message"" test", err.Message);
    }

    [Fact, NoVerify]
    public async Task ErrorMsgInsideElif()
    {
        PreprocessorException err = await Assert.ThrowsAsync<PreprocessorException>(async () => await DoTest(
@"#define NUM 1
#if NUM == 2
#elif NUM == 1
#error ""Error message""
#endif
{}"
        ));
        Assert.Equal(@"Error: ""Error message""", err.Message);
    }

    [Fact]
    public Task IfDefinedLiteral() => DoTest(
@"#define foo main
#ifdef foo
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfElseLiteral() => DoTest(
@"#define foo main
#ifndef foo
int myfoo() { return 0; }
#else
int foo() { return 0; }
#endif
");

    [Fact]
    public Task NestedIfDefined() => DoTest(
@"#define foo main
#ifdef foo
int foo() { return 0; }
#ifdef xfoo
int foo() { return 0; }
#endif
#endif
");

    [Fact]
    public Task IfNotDefinedLiteral() => DoTest(
@"#define foo main
#ifndef foo
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfValidElifInvalid() => DoTest(
@"#define num 1
#if num == 1
int ifFunc() { return 0; }
#elif num == 2
int elifFunc() { return 0; }
#endif
");

    [Fact]
    public Task IfInvalidElifValid() => DoTest(
@"#define num 1
#if num == 2
int ifFunc() { return 0; }
#elif num == 1
int elifFunc() { return 0; }
#endif
");

    [Fact]
    public Task IfInvalidTwoElifFirstElifValidSecondInvalid() => DoTest(
@"#define num 1
#if num == 2
int ifFunc() { return 0; }
#elif num == 1
int elifOneFunc() { return 0; }
#elif num == 2
int elifTwoFunc() { return 0; }
#endif
");

    [Fact]
    public Task IfInvalidTwoElifFirstElifInvalidSecondValid() => DoTest(
@"#define num 1
#if num == 2
int ifFunc() { return 0; }
#elif num == 2
int elifOneFunc() { return 0; }
#elif num == 1
int elifTwoFunc() { return 0; }
#endif
");

    [Fact]
    public Task IfInvalidAllElifInvalidElseValid() => DoTest(
@"#define num 1
#if num == 2
int ifFunc() { return 0; }
#elif num == 2
int elifOneFunc() { return 0; }
#elif num == 2
int elifTwoFunc() { return 0; }
#else
int elseFunc() { return 0; }
#endif
");

    [Fact]
    public Task IfInvalidElifInvalidElseValid() => DoTest(
@"#define num 1
#if num == 2
int ifFunc() { return 0; }
#elif num == 3
int elifFunc() { return 0; }
#else
int elseFunc() { return 0; }
#endif
");

    [Fact]
    public Task IfInvalidElifValidNestedElifValidElseInvalid() => DoTest(
@"#define NUM 9
#if NUM == 2
int ifFunc() { return 0; }
#elif NUM > 4
#if NUM == 4
int nestedIfFunc() { return 0; }
#elif NUM == 9
int nestedElifFunc() { return 0; }
#else
int nestedElseFunc() { return 0; }
#endif
#else
int elseFunc() { return 0; }
#endif
");

    [Fact]
    public Task IfInvalidElifValidNestedElifInvalidElseInvalid() => DoTest(
        @"#define NUM 9
#if NUM == 2
int ifFunc() { return 0; }
#elif NUM > 3
inf elifFunc() { return 0; }
#if NUM == 3
int nestedIfFunc() { return 0; }
#elif NUM == 1
int nestedElifFunc() { return 0; }
#endif
#else
int elseFunc() { return 0; }
#endif
");

    [Fact]
    public Task IfdefValidElifInvalid() => DoTest(
@"#define NUM 10
#ifdef NUM
int ifdefFunc() { return 1; }
#elif NUM == 10
int elifFunc() { return 1; }
#endif
");

    [Fact]
    public Task IfdefInvalidElifValid() => DoTest(
@"#define NUM 10
#ifdef NOTDEFINED
int ifdefFunc() { return 1; }
#elif NUM == 10
int elifFunc() { return 1; }
#endif
");

    [Fact]
    public Task NestedIfNotDefinedLiteral() => DoTest(
@"#define foo main
#ifndef foo1
int foo_included1() { return 0; }
#ifndef foo
int foo() { return 0; }
#endif
int foo_included2() { return 0; }
#endif
");

    [Fact]
    public Task NestedElifInInvalidIf() => DoTest(
@"#define TEST 3
#if TEST == 2
int ifFunc() { return 0; }
#if TEST == 2
int ifNestedFunc() { return 0; }
#elif TEST == 3
int main() { return 0; }
#endif
#endif
");

    [Fact]
    public Task NestedElifInInvalidIfdef() => DoTest(
@"#define TEST 3
#ifdef NOTEST
int ifFunc() { return 0; }
#if TEST == 2
int ifNestedFunc() { return 0; }
#elif TEST == 3
int main() { return 0; }
#endif
#endif
");
    [Fact]
    public Task NestedElifInValidIf() => DoTest(
@"#define TEST 3
#if TEST == 3
#if TEST == 2
int ifFunc() { return 0; }
#elif TEST == 3
int main() { return 0; }
#endif
#endif
");

    [Fact]
    public Task NestedElifInValidIfdef() => DoTest(
@"#define TEST 3
#ifdef TEST
#if TEST == 2
int ifFunc() { return 0; }
#elif TEST == 3
int main() { return 0; }
#endif
#endif
");

    [Fact]
    public Task NestedElifInInvalidIfndef() => DoTest(
@"#define TEST 3
#ifndef TEST
#ifndef TEST
int ifFunc() { return 0; }
#elif TEST == 3
int main() { return 0; }
#endif
#endif
");

    [Fact, NoVerify]
    public async Task ElifWithoutStartConditionBlockKeyWord()
    {
        var exception = await Assert.ThrowsAsync<PreprocessorException>(async () => await DoPreprocess(
            @"#elif TEST == 1
int foo() { return 0; }
#endif
"));
        Assert.Equal($"Directive such as an elif cannot exist without a directive such as if",
            exception.Message);
    }

    [Fact]
    public Task NestedElifInValidIfndef() => DoTest(
@"#define TEST 3
#ifndef NOTEST
#ifndef TEST
int ifFunc() { return 0; }
#elif TEST == 3
int main() { return 0; }
#endif
#endif
");

    [Fact]
    public Task NestedElifInElif() => DoTest(
@"#define TEST 3
#if TEST == 2
int ifFunc() { return 0; }
#elif TEST == 3
#if TEST == 2
int ifNestedFunc() { return 0; }
#elif TEST == 3
int main() { return 0; }
#endif
#endif
");

    [Fact]
    public Task ReplaceSeparatedIdentifier() => DoTest(
@"#define foo int
foo main() { return 0; }
");

    [Fact]
    public Task ReplaceNumber() => DoTest(
@"#define foo 0
int main() { return foo; }
");

    [Fact]
    public Task ReplaceAfterStar() => DoTest(
@"# define __getopt_argv_const const
int int main(char *__getopt_argv_const *___argv) { return 0; }
");

    [Fact]
    public Task ReplaceFunctionParameter() => DoTest(
@"#define foo 0
int main() { return abs(foo); }
");

    [Fact]
    public Task ReplaceWithParameter() => DoTest(
@"#define foo(x) x
int main() { return foo(0); }
");

    [Fact]
    public Task ReplaceWithParameterAndWhitespace() => DoTest(
@"#define foo(x) x
int main() { return foo (0); }
");

    [Fact]
    public Task ReplaceWithMultipleParameters() => DoTest(
@"#define foo(x,y,z) (y,z,x)
int x,test;
int main() { return foo(11,x,test); }
");

    [Fact]
    public Task ReplaceWithMultipleParameters2() => DoTest(
@"#define foo(x,y,z) (y z  x)
int x,test;
int main() { return foo(11,x,test); }
");

    [Fact]
    public Task ReplaceWithMultipleParameters3() => DoTest(
@"#define foo(x, y, z) (y,z,  x)
int x,test;
int main() { return foo(11,x,test); }
");

    [Fact]
    public Task ReplaceWithHash() => DoTest(
@"#define foo(x) #x
int main() { char* x = foo(0); }
");

    [Fact]
    public Task ReplaceWithHashNested() => DoTest(
@"#define foo(x) #x
int main() { char* x = foo(int x; printf(""some string"")); }
");

    [Fact]
    public Task ReplaceWithoutParameters() => DoTest(
@"#define foo(x)
int main() { foo(0) return 0; }
");

    [Fact, NoVerify]
    public Task IfExpressionCannotConsumeNonInteger()
    {
        return Assert.ThrowsAsync<PreprocessorException>(() => DoPreprocess(
@"#define mycondition
#if mycondition
int foo() { return 0; }
#endif
"));
    }

    [Fact]
    public Task IfExpressionEqualsLiteral() => DoTest(
@"#define mycondition 1
#if mycondition == 1
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionEqualsSkippedIfNotMetLiteral() => DoTest(
@"#define mycondition 0
#if mycondition == 1
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionNotEqualsLiteral() => DoTest(
@"#define mycondition 2
#if mycondition != 1
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionNotEqualsSkippedIfNotMetLiteral() => DoTest(
@"#define mycondition 1
#if mycondition != 1
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionGreaterOrEquals() => DoTest(
@"#define mycondition 0
#if mycondition >= 0
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionGreaterThan() => DoTest(
@"#define mycondition 0
#if mycondition > 0
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionLessOrEquals() => DoTest(
@"#define mycondition 0
#if mycondition <= 1
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionLessOrEqualsWhenNotDefined() => DoTest(
@"#if mycondition <= 1
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionDefined() => DoTest(
@"#define mycondition
#if defined mycondition
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionNotDefinedFunction() => DoTest(
@"#if !defined(not_existing_condition)
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionNotDefined() => DoTest(
@"#define mycondition
#if (!defined mycondition)
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionMultiline() => DoTest(
@"#define mycondition
#if (!defined \
    mycondition)
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionOr() => DoTest(
@"#define mycondition 0
#define mycondition2 1
#if mycondition || mycondition2
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionAnd() => DoTest(
@"#define mycondition 0
#define mycondition2 1
#if mycondition && mycondition2
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfExpressionAndWithEquality() => DoTest(
@"#define WINAPI_FAMILY_DESKTOP_APP          100
#define WINAPI_FAMILY WINAPI_FAMILY_DESKTOP_APP
#if WINAPI_FAMILY != WINAPI_FAMILY_DESKTOP_APP
int foo() { return 0; }
#endif
");

    [Fact]
    public Task IfInvalidElifdefValid() => DoTest(@"#define NUM 3
#if NUM == 2
int ifFunc() {}
#elifdef NUM
int elifdefFunc() {}
#else
int elseFunc() {}
#endif
");

    [Fact]
    public Task IfInvalidElifdefInvalidElifNDefValid() => DoTest(@"#define NUM 3
#if NUM == 2
int ifFunc() {}
#elifdef NOEXIST
int elifdefFunc() {}
#elifndef NOEXIST
int elifndefFunc() {}
#else
int elseFunc() {}
#endif
");

    [Fact]
    public Task IfInvalidElifdefValidElifNDefValid() => DoTest(@"#define NUM 3
#if NUM == 2
int ifFunc() {}
#elifdef NUM
int elifdefFunc() {}
#elifndef NOEXIST
int elifndefFunc() {}
#else
int elseFunc() {}
#endif
");

    [Fact]
    public Task IfInvalidElifdefValidElifNDefInvalid() => DoTest(@"#define NUM 3
#if NUM == 2
int ifFunc() {}
#elifdef NUM
int elifdefFunc() {}
#elifndef NUM
int elifndefFunc() {}
#else
int elseFunc() {}
#endif
");

    [Fact]
    public Task IfInvalidElifValidElifdefValid() => DoTest(@"#define NUM 3
#if NUM == 2
int ifFunc() {}
#elif NUM == 3
int elifFunc() {}
#elifdef NUM
int elifdefFunc() {}
#else
int elseFunc() {}
#endif
");

    [Fact]
    public Task UndefMacro() => DoTest(
@"#define mycondition 1
#undef mycondition
#if !(defined mycondition)
int foo() { return 0; }
#endif
");

    [Fact]
    public Task UndefMacroInElif() => DoTest(
        @"#define mycondition 1
#ifdef TEST
int ifDefFunc() { return 0; }
#elif mycondition == 1
#undef mycondition
#endif
#ifndef mycondition
int foo() { return 0; }
#endif
");

    [Fact]
    public Task ErrorInsideNotActiveBranchIsNotSupported() => DoTest(
@"#define WINAPI_FAMILY_DESKTOP_APP          100
#ifndef WINAPI_FAMILY_DESKTOP_APP
#error ""This should never happens""
#endif
");

    [Fact]
    public Task FunctionWithoutParameters() => DoTest(
@"#define x() 1
int foo() { return x(); }
");


    [Fact]
    public Task FunctionReplaceEllipsis() => DoTest(
    @"#define foo(...) __VA_ARGS__
int main() { return foo(11); }
");

    [Fact]
    public Task FunctionReplaceEllipsisMultipleParameters() => DoTest(
@"#define foo(x,...) (__VA_ARGS__,x)
int x,test;
int main() { return foo(11,x,test); }
");

    [Fact]
    public Task FunctionReplaceEllipsisMultipleParameters2() => DoTest(
@"#define foo(x,y,...) x,y,__VA_ARGS__
int x,test;
int main() { return foo(x,test,11); }
");

    [Fact]
    public Task IfExpressionDisableDefines() => DoTest(
@"#ifdef NON_EXISTS
#define foo fake_foo
#endif
int foo() { return 0; }
");

    [Fact]
    public Task DefineInElif() => DoTest(
@"#define TEST 1
#ifdef NON_EXISTS
#undef fake_foo
#elif TEST == 1
#define foo fake_foo
#endif
int fake_foo() { return 0; }
");

    [Fact]
    public Task IfExpressionDisableUnDefines() => DoTest(
@"
#define fake_foo foo

#ifdef NON_EXISTS
#undef fake_foo
#endif

int fake_foo() { return 0; }
");

    [Fact]
    public Task UnrollNestedDefines() => DoTest(
@"
#define nested_foo foo
#define _(code) nested_foo(code)
_(""test"")
");

    [Fact]
    public Task DoubleHashOperator() => DoTest(
@"
#define HASHME(x) L ## x
#define NOHASHME(x) L x
HASHME(""test"")
NOHASHME(""test"")
");

    [Fact]
    public Task LineDefine() => DoTest(
@"
int x = __LINE__;
");

    [Fact]
    public Task FileDefine() => DoTest(
@"
char* x = __FILE__;
");
}
