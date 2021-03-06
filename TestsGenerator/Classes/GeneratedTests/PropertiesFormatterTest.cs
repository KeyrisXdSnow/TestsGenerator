using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using AssemblyBrowserLib.format;

namespace AssemblyBrowserLib.format.Test
{

    [TestFixture]
    public class PropertiesFormatterTest
    {
        private PropertiesFormatter _propertiesFormatter;    
        [Test]
        public void FormatTest()
        {
            PropertyInfo propertyInfo = default;

            string actual=PropertiesFormatter.Format(propertyInfo);

            string expected = default;
            Assert.That(actual, Is.EqualTo(expected));
            Assert.Fail("autogenerated");
        }

    }
}