using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AssemblyBrowserLib.format;

namespace AssemblyBrowserLib.format.Test
{

    [TestFixture]
    public class FieldFormatterTest
    {
        [Test]
        public void FormatTest()
        {
            FieldInfo fieldInfo = default;

            string actual=FieldFormatter.Format(fieldInfo);

            string expected = default;
            Assert.That(actual, Is.EqualTo(expected));
            Assert.Fail("autogenerated");
        }

    }
}