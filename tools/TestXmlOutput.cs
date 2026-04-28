using System.Xml.Linq;
using System.Text;

class Program
{
    static void Main()
    {
        // Simulate test data
        var xml = "<Project>\n  <ItemGroup>\n    <PackageReference Include=\"Foo\" Version=\"1.0.0\" />\n  </ItemGroup>\n</Project>\n";
        var testFile = "test.xml";
        File.WriteAllText(testFile, xml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        
        // Load with PreserveWhitespace
        XDocument doc;
        using (var stream = File.OpenRead(testFile))
        {
            doc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        
        // Strip Version
        var el = doc.Root?.Descendants("PackageReference").FirstOrDefault();
        if (el != null)
            el.Attribute("Version")?.Remove();
        
        // Save to temp (like the code does)
        var tempPath = Path.GetTempFileName();
        doc.Save(tempPath, SaveOptions.None);
        var content = File.ReadAllText(tempPath, Encoding.UTF8);
        File.Delete(tempPath);
        
        Console.WriteLine("=== Restored content ===");
        Console.WriteLine(content);
        Console.WriteLine($"\n=== Starts with XML decl: {content.StartsWith("<?xml")}");
        Console.WriteLine($"=== Byte 0 (BOM check): 0x{(byte)content[0]:X2}");
        
        // Clean up
        File.Delete(testFile);
    }
}
