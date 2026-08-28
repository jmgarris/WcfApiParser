using System.Xml;
using System.Xml.Linq;
using System.Text;

namespace WcfNetTcpClientGenerator.Core;

internal static class GeneratedXmlFormatter
{
    public static FormatResult Format(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            RemoveInsignificantWhitespace(document);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = Environment.NewLine,
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = document.Declaration is null
            };

            using var writer = new Utf8StringWriter();
            using (var xmlWriter = XmlWriter.Create(writer, settings))
            {
                document.Save(xmlWriter);
            }

            return new FormatResult(writer.ToString(), null);
        }
        catch (XmlException exception)
        {
            return new FormatResult(null, exception.Message);
        }
    }

    public sealed record FormatResult(string? Xml, string? Error);

    private static void RemoveInsignificantWhitespace(XDocument document)
    {
        foreach (var text in document.DescendantNodes().OfType<XText>().Where(text => string.IsNullOrWhiteSpace(text.Value)).ToList())
        {
            if (text.Parent is not null && !PreservesWhitespace(text.Parent))
            {
                text.Remove();
            }
        }
    }

    private static bool PreservesWhitespace(XElement element)
        => element.AncestorsAndSelf().Any(candidate => string.Equals((string?)candidate.Attribute(XNamespace.Xml + "space"), "preserve", StringComparison.OrdinalIgnoreCase));

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
