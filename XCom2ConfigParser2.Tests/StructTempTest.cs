
using System;
using Xunit;
using XCom2ConfigParser2.Parser;

namespace XCom2ConfigParser2.Tests.Parser {
    public class StructTempTest {
        [Fact]
        public void TestTrailingSlashParser() {
            string text = "(Template=\"AdvTrooperShoggothM3\",             MinForceLevel=15,   MaxForceLevel=99,   MaxCharactersPerGroup=1,    SpawnWeight=2)  \\\\";
            var val = StructParser.Parse(text);
            Console.WriteLine("Parsed successfully: " + val.GetType().Name);
        }
    }
}
