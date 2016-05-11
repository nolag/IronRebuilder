using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using IronRebuilder;
using IronRebuilder.Attributes;
using IronRebuilder.CodeReplacers;
using IronRebuilder.RewriteInfo;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IronRebuilderTests
{
    [TestClass]
    public sealed class GenericEnumTests
    {
        // Chnge this if you have Reference Assemblies installed elsewhere
        private const string PortableRuntime = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETPortable\v4.5\Profile\Profile111\";

        private const string StringNotEnum = "There is no implicit reference conversion from 'string' to 'System.Enum'.";

        private const string Tbox = "The type 'T' cannot be used as type parameter 'T' in the generic type or method 'TestClass<T>'. There is no boxing conversion or type parameter conversion from 'T' to 'System.Enum'.";

        private const string Enum = @"
namespace Test
{
    public enum E
    {
        VAL
    }
}";

        private const string TestClass = @"using IronRebuilder.Attributes;
namespace Test
{
    public class TestClass<[GenericEnum]T>
    {
        public delegate object Del<[GenericEnum]U>();
        public TestClass() { }
        public virtual U GetDefault<[GenericEnum]U>()
        {
            return default(U);
        }

        public static U GetDefaultS<[GenericEnum]U>()
        {
            return default(U);
        }

        public bool MethodWithGeneric<[GenericEnum]T2, [GenericEnum]T3>(T a1, T2 a2, T3 a3)
        {
            return a1.Equals(default(T)) || a2.Equals(default(T)) || a3.Equals(default(T3));
        }

        public static bool MethodWithGenericS<[GenericEnum]T2, [GenericEnum]T3>(T a1, T2 a2, T3 a3)
        {
            return a1.Equals(default(T)) || a2.Equals(default(T)) || a3.Equals(default(T3));
        }

        public class InnerClass<[GenericEnum] Z> { }
    }
}";

        private const string TestClass2 = @"using IronRebuilder.Attributes;
namespace Test
    {
        public class TestClass2<[GenericEnum]T>
        {
            private static TestClass<T> tcs;
            private TestClass<T> tc;
            public static TestClass<T> Tcs { get { return tcs; } }
            public TestClass<T> Tc { get { return tc; } }

            public TestClass2()
            {
                tc = null;
                tcs = null;
            }
        }
    }";

        private const string TestInterface = @"using IronRebuilder.Attributes;

namespace Test
{
    public interface TestInterface<[GenericEnum]T>
    {
        void Method();
    }
}";

        private enum TestEnum
        {
            VAL
        }

        [TestMethod]
        public void BasicEndToEndTestWithSig()
        {
            var path = Path.GetTempFileName();
            var path2 = Path.GetTempFileName();
            using (var fileStream = new FileStream(path, FileMode.OpenOrCreate))
            {
                Compile(new[] { TestClass }, fileStream, null);
            }

            var metaDataRef = MetadataReference.CreateFromFile(path);

            using (var fileStream = new FileStream(path2, FileMode.OpenOrCreate))
            {
                Compile(new[] { TestClass2 }, fileStream, metaDataRef);
            }

            var args = new[] { "-f", string.Join(";", path, path2), "-s", Path.Combine("TestItems", "TestKey.snk") };

            Assert.AreEqual(0, Program.Main(args));

            TestFileWithGenericEnumClassAndSig(path, "Test.TestClass`1");
            TestFileWithGenericEnumClassAndSig(path2, "Test.TestClass2`1");
        }

        [TestMethod]
        public void TestVaildClass()
        {
            var extendingClass = @"
namespace Test
{
    public class Class : TestClass<E>
    {
        public Class() { }
    }
}";

            var usingClass = @"
namespace Test
{
    public class Class2
    {
        public Class2() { }

        public TestClass<E> TestMake()
        {
            return new TestClass<E>();
        }

        public static TestClass<E> TestMakeS()
        {
            return new TestClass<E>();
        }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.Class", true);
                var @class = Activator.CreateInstance(type);
                type = a.GetType("Test.Class2", true);
                var staticMethod = type.GetMethod("TestMakeS");
                staticMethod.Invoke(null, null);
                var memberMethod = type.GetMethod("TestMake");
                var class2 = Activator.CreateInstance(type);
                memberMethod.Invoke(class2, null);
            };

            TestValid(new[] { TestClass }, new[] { Enum, extendingClass, usingClass }, verify);
        }

        [TestMethod]
        public void TestInvaildClassExtended()
        {
            var extendingClass = @"
namespace Test
{
    public class Class : TestClass<string>
    {
        public Class() { }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestMethodOverrideImpliedCorrectly()
        {
            var extendingClass = @"
namespace Test
{
    public class Class : TestClass<E>
    {
        public Class() { }

        public override U GetDefault<U>()
        {
            return default(U);
        }
    }
}";
            TestValid(new[] { TestClass }, new[] { Enum, extendingClass }, a => { });

            extendingClass = @"
namespace Test
{
    public class Class : TestClass<E>
    {
        public Class()
        {
            GetDefault<string>();
        }

        public override U GetDefault<U>()
        {
            return default(U);
        }
    }
}";

            var err = "The type 'string' cannot be used as type parameter 'U' in the generic type or method 'Class.GetDefault<U>()'. There is no implicit reference conversion from 'string' to 'System.Enum'.";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, err);
        }

        [TestMethod]
        public void TestInvaildClassAllowsAnyExtend()
        {
            var extendingClass = @"
namespace Test
{
    public class Class<T> : TestClass<T>
    {
        public Class() { }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, Tbox);
        }

        [TestMethod]
        public void TestInvaildParameterTooGeneric()
        {
            var extendingClass = @"
namespace Test
{
    public class Class<T>
    {
        public TestClass<T> p;
        public static TestClass<T> ps;
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, Tbox, Tbox);
        }

        [TestMethod]
        public void TestStructsWorkLikeClass()
        {
            var extendingClass = @"
namespace Test
{
    public struct Structure<T>
    {
        public TestClass<T> p;
        public static TestClass<T> ps;
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, Tbox, Tbox);
        }

        [TestMethod]
        public void TestInvaildParameterString()
        {
            var extendingClass = @"
namespace Test
{
    public class Class
    {
        public TestClass<string> p;
        public static TestClass<string> ps;
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, StringNotEnum, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildGetSetTooGeneric()
        {
            var extendingClass = @"
namespace Test
{
    public class Class<T>
    {
        public TestClass<T> P { get; set; }
        public static TestClass<T> Ps { get; set; }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, Tbox, Tbox);
        }

        [TestMethod]
        public void TestInvaildGetSetString()
        {
            var extendingClass = @"
namespace Test
{
    public class Class
    {
        public TestClass<string> P { get; set; }
        public static TestClass<string> Ps { get; set; }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, StringNotEnum, StringNotEnum);
        }

        [TestMethod]
        public void TestVaildGetSet()
        {
            var testCalss = @"
namespace Test
{
    public class Class
    {
        public TestClass<E> P { get; set; }
        public static TestClass<E> Ps { get; set; }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.Class", true);
                var member = type.GetProperty("P");
                var member2 = type.GetProperty("Ps");
                var runTest = Activator.CreateInstance(type);
                member.GetValue(runTest, null);
                member2.GetValue(runTest, null);
            };

            TestValid(new[] { TestClass }, new[] { Enum, testCalss }, verify);
        }

        [TestMethod]
        public void TestInvaildArgTooGenericOnClass()
        {
            var testClass = @"
namespace Test
{
    public class Class<T>
    {
        public void Method(TestClass<T> arg) { }
        public void MethodS(TestClass<T> arg) { }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, testClass }, Tbox, Tbox);
        }

        [TestMethod]
        public void TestInvaildArgTooGenericMethod()
        {
            var testClass = @"
namespace Test
{
    public class Class
    {
        public void Method<T>(TestClass<T> arg) { }
        public void MethodS<T>(TestClass<T> arg) { }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, testClass }, Tbox, Tbox);
        }

        [TestMethod]
        public void TestInvaildArgString()
        {
            var testCalss = @"
namespace Test
{
    public class Class
    {
        public void Method(TestClass<string> arg) { }
        public void MethodS(TestClass<string> arg) { }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, testCalss }, StringNotEnum, StringNotEnum);
        }

        [TestMethod]
        public void TestVaildArgs()
        {
            var testCalss = @"
namespace Test
{
    public class Class
    {
        public Class() { }
        public void Method(TestClass<E> arg) { }
        public void MethodS(TestClass<E> arg) { }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.Class", true);
                var member = type.GetMethod("Method");
                var member2 = type.GetMethod("MethodS");
                var runTest = Activator.CreateInstance(type);
                member.Invoke(runTest, new object[] { null });
                member2.Invoke(runTest, new object[] { null });
            };

            TestValid(new[] { TestClass }, new[] { Enum, testCalss }, verify);
        }

        public void TestVaildArgsInClass()
        {
            var testCalss = @"
namespace Test
{
    public class Class<[GenericEnum]T>
    {
        public void Method(TestClass<T> arg) { }
        public void MethodS(TestClass<T> arg) { }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.Class", true);
                var member = type.GetMethod("Method");
                var member2 = type.GetMethod("Methods");
                var runTest = Activator.CreateInstance(type);
                member.Invoke(runTest, null);
                member2.Invoke(runTest, null);
            };

            // Since C# won't honour the generic enum, this will only work if compiled in the same assebly
            TestValid(new[] { TestClass, Enum, testCalss }, Enumerable.Empty<string>(), verify);
        }

        public void TestVaildArgsMethod()
        {
            var testCalss = @"
namespace Test
{
    public class Class
    {
        public void Method<[GenericEnum]T>(TestClass<T> arg) { }
        public void MethodS<[GenericEnum]T>(TestClass<T> arg) { }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.Class", true);
                var member = type.GetMethod("Method");
                var member2 = type.GetMethod("Methods");
                var runTest = Activator.CreateInstance(type);
                member.Invoke(runTest, null);
                member2.Invoke(runTest, null);
            };

            // Since C# won't honour the generic enum, this will only work if compiled in the same assebly
            TestValid(new[] { TestClass, Enum, testCalss }, Enumerable.Empty<string>(), verify);
        }

        [TestMethod]
        public void TestInvaildMethodAllowsAnyExtend()
        {
            var extendingClass = @"
namespace Test
{
    public class Class
    {
        public Class() { }

        public TestClass<T> Make<T>()
        {
            return null;
        }

        public static TestClass<T> Makes<T>()
        {
            return null;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, Tbox, Tbox);
        }

        [TestMethod]
        public void TestInvaildMethodAllowsAnyExtendInArg()
        {
            var extendingClass = @"
namespace Test
{
    public class Class
    {
        public Class() { }

        public void Make<T>(TestClass<T> tc)
        {
            return;
        }

        public static void Makes<T>(TestClass<T> tc)
        {
            return;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, extendingClass }, Tbox, Tbox);
        }

        [TestMethod]
        public void TestInvaildClassUse()
        {
            var usingClass = @"
namespace Test
{
    public class Class2
    {
        public TestClass<string> TestMake()
        {
            return null;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildUseAsArg()
        {
            var usingClass = @"
using System.Collections.Generic;

namespace Test
{
    public class Class2
    {
        public void TestMake()
        {
            var objs = new List<object>();
            objs.Add(new TestClass<string>());
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildUseNested()
        {
            var usingClass = @"
using System.Collections.Generic;

namespace Test
{
    public class Class2
    {
        public void TestMake()
        {
            var objs = new List<TestClass<string>>();
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildUseAsCast()
        {
            var usingClass = @"
using System.Collections.Generic;

namespace Test
{
    public class Class2
    {
        public void TestMake()
        {
            object o = ""lol"";
            object o2 = (TestClass<string>)o;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildUseInAs()
        {
            var usingClass = @"
using System.Collections.Generic;

namespace Test
{
    public class Class2
    {
        public void TestMake()
        {
            object o = ""lol"";
            object o2 = o as TestClass<string>;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildSecondAssembly()
        {
            var testClassBad = @"namespace Test
{
    public class TestClass2<T> : TestClass<T>
    {
    }
}";
            var path = Path.GetTempFileName();

            using (FileStream stream = new FileStream(path, FileMode.Create))
            {
                Compile(new[] { TestClass }, stream, null);
            }

            var metaDataRef = MetadataReference.CreateFromFile(path);

            using (MemoryStream stream2 = new MemoryStream())
            {
                Compile(new[] { testClassBad }, stream2, metaDataRef);
                var rewriteInfos = new IRewriteInfo[]
                {
                    new FileRewriteInfo(path, true),
                    new InMemoryRewriteInfo(stream2.ToArray())
                };

                var errs = new List<string>();
                var replacements = new List<ICodeReplacer>();
                replacements.Add(new GenericEnum(errs.Add));
                Assert.IsFalse(Core.Rebuild(rewriteInfos, replacements, null));
            }
        }

        [TestMethod]
        public void TestInvaildClassUseStatic()
        {
            var usingClass = @"
namespace Test
{
    public class Class2
    {
        public static TestClass<string> TestMake()
        {
            return null;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, StringNotEnum);
        }

        public void TestInvaildClassUseInstance()
        {
            var usingClass = @"
namespace Test
{
    public class Class2
    {
        public void TestMake()
        {
            new TestClass<string>();
        }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestVaildMethodReturn()
        {
            var usingClass = @"
namespace Test
{
    public class RunTest
    {
        public RunTest() { }

        public E TestMake()
        {
            return new TestClass<E>().GetDefault<E>();
        }

        public E TestMake2()
        {
            return TestClass<E>.GetDefaultS<E>();
        }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.RunTest", true);
                var memberMethod = type.GetMethod("TestMake");
                var memberMethod2 = type.GetMethod("TestMake2");
                var runTest = Activator.CreateInstance(type);
                memberMethod.Invoke(runTest, null);
                memberMethod2.Invoke(runTest, null);
            };

            TestValid(new[] { TestClass }, new[] { Enum, usingClass }, verify);
        }

        [TestMethod]
        public void TestInvaildMethodReturn()
        {
            var usingClass = @"
namespace Test
{
    public class RunTest
    {
        public string TestMake()
        {
            return new TestClass<E>().GetDefault<string>();
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildMethodReturnTooGeneric()
        {
            var usingClass = @"
namespace Test
{
    public class RunTest
    {
        public T TestMake<T>()
        {
            return new TestClass<E>().GetDefault<T>();
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, "The type 'T' cannot be used as type parameter 'U' in the generic type or method 'TestClass<E>.GetDefault<U>()'. There is no boxing conversion or type parameter conversion from 'T' to 'System.Enum'.");
        }

        [TestMethod]
        public void TestInvaildStaticMethodReturn()
        {
            var usingClass = @"
namespace Test
{
    public class InvaildStaticMethodReturn
    {
        public string TestMake()
        {
            return TestClass<E>.GetDefaultS<string>();
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestVaildNestedClass()
        {
            var usingClass = @"
namespace Test
{
    public class VaildNestedClass
    {
        public VaildNestedClass() { }

        public TestClass<E>.InnerClass<E> TestMake()
        {
            return new TestClass<E>.InnerClass<E>();
        }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.VaildNestedClass", true);
                var memberMethod = type.GetMethod("TestMake");
                var nestedCreated = Activator.CreateInstance(type);
                memberMethod.Invoke(nestedCreated, null);
            };

            TestValid(new[] { TestClass }, new[] { Enum, usingClass }, verify);
        }

        [TestMethod]
        public void TestInvaildNestedClass()
        {
            var usingClass = @"
namespace Test
{
    public class InvaildNestedClass
    {
        public TestClass<E>.InnerClass<string> TestMake()
        {
            return new TestClass<E>.InnerClass<string>();
        }
    }
}";

            var stringNotEnumz = "The type 'string' cannot be used as type parameter 'Z' in the generic type or method 'TestClass<E>.InnerClass<Z>'. There is no implicit reference conversion from 'string' to 'System.Enum'.";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingClass }, stringNotEnumz, stringNotEnumz);
        }

        [TestMethod]
        public void TestVaildMultipleParams()
        {
            var usingMethod = @"
namespace Test
{
    public class TestVaildMultipleParams
    {
        public TestVaildMultipleParams() { }

        public void TestMake()
        {
            new TestClass<E>().MethodWithGeneric<E, E>(E.VAL, E.VAL, E.VAL);
            TestClass<E>.MethodWithGenericS<E, E>(E.VAL, E.VAL, E.VAL);
        }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.TestVaildMultipleParams", true);
                var memberMethod = type.GetMethod("TestMake");
                var validMultiParamUse = Activator.CreateInstance(type);
                memberMethod.Invoke(validMultiParamUse, null);
            };

            TestValid(new[] { TestClass }, new[] { Enum, usingMethod }, verify);
        }

        [TestMethod]
        public void TestInvaildMultipleParams()
        {
            var usingMethod = @"
namespace Test
{
    public class VaildMultipleParams
    {
        public void TestMake()
        {
            new TestClass<E>().MethodWithGeneric<string, string>(E.VAL, null, null);
            TestClass<E>.MethodWithGenericS<string, string>(E.VAL, null, null);
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { Enum, usingMethod }, StringNotEnum, StringNotEnum, StringNotEnum, StringNotEnum);
        }

        [TestMethod]
        public void TestValidTExtendsStruct()
        {
            var structExtend = @"using IronRebuilder.Attributes;
namespace Test
{
    public class ValidTExtendsStruct<[GenericEnum]T> where T : struct
    {
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.ValidTExtendsStruct`1", true);
                type = type.MakeGenericType(new[] { typeof(TestEnum) });
                var validMultiParamUse = Activator.CreateInstance(type);
            };

            TestValid(new[] { structExtend }, Enumerable.Empty<string>(), verify);
        }

        [TestMethod]
        public void TestInvalidTExtendsClass()
        {
            var @base = @"
namespace Test
{
    public class Base
    {
    }
}";

            var extended = @"using IronRebuilder.Attributes;
namespace Test
{
    public class ValidTExtendsStruct<[GenericEnum]T> where T : Base
    {
    }
}";
            Test(new[] { @base, extended }, Enumerable.Empty<string>(), false, false, errMessages: "The type 'Base' cannot be used as type parameter 'T' in the generic type or method 'TestClass<T>'. There is no implicit reference conversion from 'Base' to 'System.Enum'.");
        }

        [TestMethod]
        public void TestVaildInterface()
        {
            var extendingClass = @"using System;
namespace Test
{
    public class ClassWithInterface : TestInterface<E>
    {
        public void Method()
        {
        }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.ClassWithInterface", true);
                var @class = Activator.CreateInstance(type);
                var memberMethod = type.GetMethod("Method");
                memberMethod.Invoke(@class, null);
            };

            TestValid(new[] { TestInterface }, new[] { Enum, extendingClass }, verify);
        }

        [TestMethod]
        public void TestInvaildInterfaceExtended()
        {
            var extendingClass = @"using System;
namespace Test
{
    public class Class : TestInterface<string>
    {
        public void Method()
        {
        }
    }
}";
            TestInvalidDepends(new[] { TestInterface }, new[] { Enum, extendingClass }, StringNotEnum);
        }

        [TestMethod]
        public void TestValidArray()
        {
            var arrayVal = @"using IronRebuilder.Attributes;
namespace Test
{
    public class TestValidArray
    {
        public static TestClass<E>[] TestMakeS()
        {
            return new TestClass<E>[5];
        }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.TestValidArray", true);
                var staticMethod = type.GetMethod("TestMakeS");
                staticMethod.Invoke(null, null);
            };

            TestValid(new[] { TestClass }, new[] { Enum, arrayVal }, verify);
        }

        [TestMethod]
        public void TestInvaildArrayInReturn()
        {
            var invalidArray = @"using System;
namespace Test
{
    public class Class
    {
        public static object TestMakeS()
        {
            return new TestClass<string>[5];
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { invalidArray }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildArrayInParam()
        {
            var invalidArray = @"using System;
namespace Test
{
    public class Class
    {
        public static object TestMakeS(TestClass<string>[] arg)
        {
            return arg != null;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { invalidArray }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildArrayAsReturn()
        {
            var invalidArray = @"using System;
namespace Test
{
    public class Class
    {
        public static TestClass<string>[] TestMakeS()
        {
            return null;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { invalidArray }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildArrayAsReturnedObj()
        {
            var invalidArray = @"using System;
namespace Test
{
    public class Class
    {
        public static object TestMakeS()
        {
            return new TestClass<string>[0];
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { invalidArray }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildArrayAsVariable()
        {
            var invalidArray = @"using System;
namespace Test
{
    public class Class
    {
        public static object TestMakeS(object s)
        {
            TestClass<string>[] x = null;
            return s == x;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { invalidArray }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildOutParam()
        {
            var invalidArray = @"using System;
namespace Test
{
    public class Class
    {
        public static void TestMakeS(out TestClass<string> oval)
        {
            oval = null;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { invalidArray }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildParam()
        {
            var invalidArray = @"using System;
namespace Test
{
    public class Class
    {
        public static void TestMakeS(TestClass<string> oval)
        {
            oval = null;
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { invalidArray }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildArrayAsObjVariable()
        {
            var invalidArray = @"using System;
namespace Test
{
    public class Class
    {
        public static void TestMakeS(object s)
        {
            object x = new TestClass<string>[0];
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { invalidArray }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvaildTypeOf()
        {
            var invalidArray = @"using System;
namespace Test
{
    public class Class
    {
        public static Type TestMakeS(object s)
        {
            return typeof(TestClass<string>);
        }
    }
}";
            TestInvalidDepends(new[] { TestClass }, new[] { invalidArray }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvalidDelegateTooGenericFromClass()
        {
            var classWithDelegate = @"
namespace Test
{
    public struct ClassWithDelegate<T>
    {
        public delegate void Del(TestClass<T> tc);
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { classWithDelegate }, Tbox);
        }

        [TestMethod]
        public void TestInvalidDelegateTooGeneric()
        {
            var classWithDelegate = @"
namespace Test
{
    public struct ClassWithDelegate
    {
        public delegate void Del<T>(TestClass<T> tc);
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { classWithDelegate }, Tbox);
        }

        [TestMethod]
        public void TestInvalidDelegateString()
        {
            var classWithDelegate = @"
namespace Test
{
    public struct ClassWithDelegate
    {
        public delegate void Del(TestClass<string> tc);
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { classWithDelegate }, StringNotEnum);
        }

        [TestMethod]
        public void TestValidDelegate()
        {
            var classWithDelegate = @"
namespace Test
{
    public struct ClassWithDelegate
    {
        public delegate void Del(TestClass<E> tc);
    }
}";

            TestValid(new[] { TestClass }, new[] { Enum, classWithDelegate }, a => { /* not sure how to verify this one */ });
        }

        [TestMethod]
        public void TestValidDelegateWithEvent()
        {
            var classWithDelegate = @"using IronRebuilder.Attributes;
namespace Test
{
    public struct ClassWithDelegate
    {
        public event TestClass<E>.Del<E> MyEvent;
        public void Stuff()
        {
            MyEvent();
        }
    }
}";

            TestValid(new[] { TestClass }, new[] { Enum, classWithDelegate }, a => { /* not sure how to verify this one */ });
        }

        [TestMethod]
        public void TestInvalidDelegateWithEvent()
        {
            var classWithDelegate = @"
namespace Test
{
    public struct ClassWithDelegate<T>
    {
        public event TestClass<E>.Del<T> MyEvent;
        public void Stuff()
        {
            MyEvent();
        }
    }
}";

            TestInvalidDepends(
                new[] { TestClass },
                new[] { Enum, classWithDelegate },
                "The type 'T' cannot be used as type parameter 'U' in the generic type or method 'TestClass<E>.Del<U>'. There is no boxing conversion or type parameter conversion from 'T' to 'System.Enum'.");
        }

        [TestMethod]
        public void TestInvalidDelegateSringWithEvent()
        {
            var classWithDelegate = @"
namespace Test
{
    public struct ClassWithDelegate
    {
        public event TestClass<E>.Del<string> MyEvent;
        public void Stuff()
        {
            MyEvent();
        }
    }
}";

            TestInvalidDepends(
                new[] { TestClass },
                new[] { Enum, classWithDelegate },
                "The type 'string' cannot be used as type parameter 'U' in the generic type or method 'TestClass<E>.Del<U>'. There is no implicit reference conversion from 'string' to 'System.Enum'.");
        }

        [TestMethod]
        public void TestValidDelegateAlsoGenEnum()
        {
            var classWithDelegate = @"using IronRebuilder.Attributes;
namespace Test
{
    public struct ClassWithDelegate
    {
        public delegate void Del<[GenericEnum]T>(TestClass<T> tc);
    }
}";

            TestValid(new[] { TestClass, classWithDelegate }, Enumerable.Empty<string>(), a => { /* not sure how to verify this one */ });
        }

        [TestMethod]
        public void TestInvalidActionSring()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithAction
    {
        public static void Stuff()
        {
            Action a = () => new TestClass<String>();
            a();
        }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, classWtihFn }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvalidActionTooGeneric()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithAction
    {
        public static void Stuff<T>()
        {
            Action a = () => new TestClass<T>();
            a();
        }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, classWtihFn }, Tbox);
        }

        [TestMethod]
        public void TestValidAction()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithAction
    {
        public static void Stuff()
        {
            Action a = () => new TestClass<E>();
            a();
        }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.ClassWithAction", true);
                var staticMethod = type.GetMethod("Stuff");
                staticMethod.Invoke(null, null);
            };

            TestValid(new[] { TestClass }, new[] { Enum, classWtihFn }, verify);
        }

        [TestMethod]
        public void TestInvalidActionSringParam()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithAction
    {
        public static Action A = () => new TestClass<string>();
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, classWtihFn }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvalidActionTooGenericParam()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithAction<T>
    {
        public static Action A = () => new TestClass<T>();
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, classWtihFn }, Tbox);
        }

        [TestMethod]
        public void TestValidActionParam()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithAction
    {
        public static Action A = () => new TestClass<E>();
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.ClassWithAction", true);
                Action action = type.GetField("A", BindingFlags.Static | BindingFlags.Public).GetValue(null) as Action;
                action();
            };

            TestValid(new[] { TestClass }, new[] { Enum, classWtihFn }, verify);
        }

        [TestMethod]
        public void TestInvalidFuncSring()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithFn
    {
        public static void Stuff()
        {
            Func<object> a = () => new TestClass<String>();
            a();
        }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, classWtihFn }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvalidFuncTooGeneric()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithFn
    {
        public static void Stuff<T>()
        {
            Func<object> a = () => new TestClass<T>();
            a();
        }
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, classWtihFn }, Tbox);
        }

        [TestMethod]
        public void TestValidFunc()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithFn
    {
        public static void Stuff()
        {
            Func<object> a = () => new TestClass<E>();
            a();
        }
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.ClassWithFn", true);
                var staticMethod = type.GetMethod("Stuff");
                staticMethod.Invoke(null, null);
            };

            TestValid(new[] { TestClass }, new[] { Enum, classWtihFn }, verify);
        }

        [TestMethod]
        public void TestInvalidFuncSringParam()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithFn
    {
        public static Func<object> A = () => new TestClass<string>();
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, classWtihFn }, StringNotEnum);
        }

        [TestMethod]
        public void TestInvalidFuncTooGenericParam()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithFn<T>
    {
        public static Func<object> A = () => new TestClass<T>();
    }
}";

            TestInvalidDepends(new[] { TestClass }, new[] { Enum, classWtihFn }, Tbox);
        }

        [TestMethod]
        public void TestValidFuncParam()
        {
            var classWtihFn = @"using System;
namespace Test
{
    public struct ClassWithFn
    {
        public static Func<object> A = () => new TestClass<E>();
    }
}";

            Action<Assembly> verify = a =>
            {
                // Test that we can use the resulting class and methods
                var type = a.GetType("Test.ClassWithFn", true);
                Func<object> func = type.GetField("A", BindingFlags.Static | BindingFlags.Public).GetValue(null) as Func<object>;
                func();
            };

            TestValid(new[] { TestClass }, new[] { Enum, classWtihFn }, verify);
        }

        private static EmitResult Compile(IEnumerable<string> sources, Stream stream, MetadataReference alsoInclude)
        {
            var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
            var assemblyName = Path.GetRandomFileName();
            var references = new List<MetadataReference>();
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(GenericEnumAttribute).Assembly.Location));
            references.AddRange(Directory.GetFiles(PortableRuntime, "*.dll")
                .Select(dll => MetadataReference.CreateFromFile(dll)));

            if (alsoInclude != null) references.Add(alsoInclude);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: trees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var results = compilation.Emit(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return results;
        }

        private static void TestValid(IEnumerable<string> rebuildSources, IEnumerable<string> dependatSources, Action<Assembly> verify)
        {
            Test(rebuildSources.Union(dependatSources), Enumerable.Empty<string>(), true, false, verify);
            if (dependatSources.Count() != 0)
            {
                Test(rebuildSources, dependatSources, true, true, verify);
            }
        }

        private static void TestInvalidDepends(IEnumerable<string> rebuildSources, IEnumerable<string> dependatSources, params string[] errors)
        {
            Test(rebuildSources, dependatSources, true, false, errMessages: errors);
            Test(rebuildSources.Union(dependatSources), Enumerable.Empty<string>(), false, false, errMessages: errors);
        }

        private static void Test(IEnumerable<string> rebuildSources, IEnumerable<string> dependatSources, bool validRebuild, bool validDepend, Action<Assembly> verify = null, params string[] errMessages)
        {
            MetadataReference reference = null;
            var errs = new List<string>();
            InMemoryRewriteInfo memoryRewrite;

            using (var inStream = new MemoryStream())
            {
                var replacements = new List<ICodeReplacer>();
                replacements.Add(new GenericEnum(errs.Add));
                Assert.IsTrue(Compile(rebuildSources, inStream, null).Success);
                memoryRewrite = new InMemoryRewriteInfo(inStream.ToArray());
                Assert.AreEqual(validRebuild, Core.Rebuild(new[] { memoryRewrite }, replacements, null));
                if (validRebuild)
                {
                    reference = MetadataReference.CreateFromImage(
                        ImmutableArray.Create(memoryRewrite.GetWrittenValue()));
                }
                else
                {
                    // getters and setters can set 2 messages instead of 1
                    Assert.IsTrue(errMessages.Length <= errs.Count);
                }
            }

            var rebuildAssembly = memoryRewrite.GetWrittenValue();

            if (dependatSources.Count() != 0)
            {
                using (var dependStream = new MemoryStream())
                {
                    var results = Compile(dependatSources, dependStream, reference);
                    Assert.AreEqual(validDepend, results.Success);
                    if (!validDepend)
                    {
                        Assert.AreEqual(errMessages.Length, results.Diagnostics.Length);
                        Assert.IsTrue(results.Diagnostics.Select(d => d.GetMessage()).All(e => errMessages.Any(e.EndsWith)));
                        return;
                    }

                    ResolveEventHandler handler = (sender, args) => Assembly.Load(rebuildAssembly);
                    AppDomain.CurrentDomain.AssemblyResolve += handler;

                    // put in try/finally so that we will always remove the handler and not break later tests
                    try
                    {
                        verify(Assembly.Load(dependStream.ToArray()));
                    }
                    finally
                    {
                        AppDomain.CurrentDomain.AssemblyResolve -= handler;
                    }
                }
            }
        }

        private static void TestFileWithGenericEnumClassAndSig(string path, string className)
        {
            var loaded = Assembly.LoadFile(path);
            var testType = loaded.GetType(className, true);
            Assert.AreEqual(0, testType.CustomAttributes.Count());
            var genParams = testType.GetTypeInfo().GenericTypeParameters.ToArray();
            Assert.AreEqual(1, genParams.Length);
            var constraints = genParams[0].GetGenericParameterConstraints();
            Assert.AreEqual(1, constraints.Length);
            Assert.AreEqual("System.Enum", constraints[0].FullName);
            Assert.AreEqual(typeof(object).Assembly, constraints[0].Assembly);
            var publicKey = File.ReadAllBytes(Path.Combine("TestItems", "TestKeyPublic.snk"));
            CollectionAssert.AreEqual(publicKey, loaded.GetName().GetPublicKey());
        }
    }
}
