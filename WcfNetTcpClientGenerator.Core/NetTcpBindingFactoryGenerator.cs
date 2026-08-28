using System.Text;

namespace WcfNetTcpClientGenerator.Core;

public sealed class NetTcpBindingFactoryGenerator
{
    private static readonly HashSet<string> SupportedSecurityModes =
    [
        "None",
        "Transport",
        "Message",
        "TransportWithMessageCredential"
    ];

    private static readonly HashSet<string> SupportedCredentialTypes =
    [
        "None",
        "Windows",
        "Certificate",
        "UserName"
    ];

    public IReadOnlyList<GenerationDiagnostic> ValidateOptions(ClientLibraryGenerationOptions options)
    {
        var diagnostics = new List<GenerationDiagnostic>();

        if (!SupportedSecurityModes.Contains(options.SecurityMode))
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                $"Unsupported security mode: {options.SecurityMode}",
                "UNSUPPORTED_SECURITY_MODE"));
        }

        var transportCredentialType = options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper
            ? options.TcpTransportClientCredentialType : options.TcpClientCredentialType;
        if (!SupportedCredentialTypes.Contains(transportCredentialType))
        {
            diagnostics.Add(new GenerationDiagnostic(
                DiagnosticSeverity.Error,
                $"Unsupported TCP client credential type: {transportCredentialType}",
                "UNSUPPORTED_CREDENTIAL_TYPE"));
        }

        if (!SupportedCredentialTypes.Contains(options.MessageClientCredentialType))
        {
            diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Error, $"Unsupported message client credential type: {options.MessageClientCredentialType}", "UNSUPPORTED_MESSAGE_CREDENTIAL_TYPE"));
        }

        var usesCertificate = options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper
            && (string.Equals(options.TcpTransportClientCredentialType, "Certificate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(options.MessageClientCredentialType, "Certificate", StringComparison.OrdinalIgnoreCase));
        if (usesCertificate && string.Equals(options.ClientCertificateSource, "Store", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.ClientCertificateFindValue))
        {
            diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Error, "A certificate find value is required when using the Windows certificate store.", "CERTIFICATE_FIND_VALUE_REQUIRED"));
        }
        if (usesCertificate && string.Equals(options.ClientCertificateSource, "File", StringComparison.OrdinalIgnoreCase))
        {
            var extension = Path.GetExtension(options.ClientCertificateFilePath);
            if (string.IsNullOrWhiteSpace(options.ClientCertificateFilePath)) diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Error, "A certificate file path is required.", "CERTIFICATE_FILE_REQUIRED"));
            else if (!new[] { ".pfx", ".p12", ".cer", ".crt" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Error, "Certificate files must be .pfx, .p12, .cer, or .crt.", "CERTIFICATE_FILE_EXTENSION_INVALID"));
            else if (!File.Exists(options.ClientCertificateFilePath)) diagnostics.Add(new GenerationDiagnostic(DiagnosticSeverity.Warning, "The certificate file does not exist at the selected path.", "CERTIFICATE_FILE_NOT_FOUND"));
        }

        return diagnostics;
    }

    public string Generate(string libraryNamespace, ClientLibraryGenerationOptions options)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"using {libraryNamespace}.{(options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper ? "Wcf" : "Options")};");
        builder.AppendLine();
        builder.AppendLine($"namespace {libraryNamespace}.Binding;");
        builder.AppendLine();
        builder.AppendLine("public static class NetTcpBindingFactory");
        builder.AppendLine("{");
        builder.AppendLine("    public static global::System.ServiceModel.NetTcpBinding Create(NetTcpWcfClientOptions options)");
        builder.AppendLine("    {");
        builder.AppendLine("        var binding = new global::System.ServiceModel.NetTcpBinding");
        builder.AppendLine("        {");
        builder.AppendLine("            Security =");
        builder.AppendLine("            {");
        builder.AppendLine("                Mode = MapSecurityMode(options.SecurityMode),");
        builder.AppendLine("                Transport =");
        builder.AppendLine("                {");
        builder.AppendLine($"                    ClientCredentialType = MapCredentialType(options.{(options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper ? "TcpTransportClientCredentialType" : "TcpClientCredentialType")})");
        builder.AppendLine("                }");
        if (options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper)
        {
            builder.AppendLine("                , Message = { ClientCredentialType = MapMessageCredentialType(options.MessageClientCredentialType) }");
        }
        builder.AppendLine("            },");
        builder.AppendLine("            ReliableSession =");
        builder.AppendLine("            {");
        builder.AppendLine("                Enabled = options.ReliableSessionEnabled");
        builder.AppendLine("            },");
        builder.AppendLine("            OpenTimeout = options.OpenTimeout,");
        builder.AppendLine("            CloseTimeout = options.CloseTimeout,");
        builder.AppendLine("            SendTimeout = options.SendTimeout,");
        builder.AppendLine("            ReceiveTimeout = options.ReceiveTimeout,");
        builder.AppendLine("            MaxReceivedMessageSize = options.MaxReceivedMessageSize");
        builder.AppendLine("        };");
        builder.AppendLine();
        builder.AppendLine("        return binding;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static global::System.ServiceModel.SecurityMode MapSecurityMode(string value)");
        builder.AppendLine("        => value switch");
        builder.AppendLine("        {");
        builder.AppendLine("            \"None\" => global::System.ServiceModel.SecurityMode.None,");
        builder.AppendLine("            \"Transport\" => global::System.ServiceModel.SecurityMode.Transport,");
        builder.AppendLine("            \"Message\" => global::System.ServiceModel.SecurityMode.Message,");
        builder.AppendLine("            \"TransportWithMessageCredential\" => global::System.ServiceModel.SecurityMode.TransportWithMessageCredential,");
        builder.AppendLine("            _ => throw new global::System.ArgumentOutOfRangeException(nameof(value), $\"Unsupported security mode: {value}\")");
        builder.AppendLine("        };");
        builder.AppendLine();
        builder.AppendLine("    private static global::System.ServiceModel.TcpClientCredentialType MapCredentialType(string value)");
        builder.AppendLine("        => value switch");
        builder.AppendLine("        {");
        builder.AppendLine("            \"None\" => global::System.ServiceModel.TcpClientCredentialType.None,");
        builder.AppendLine("            \"Windows\" => global::System.ServiceModel.TcpClientCredentialType.Windows,");
        builder.AppendLine("            \"Certificate\" => global::System.ServiceModel.TcpClientCredentialType.Certificate,");
        builder.AppendLine("            \"UserName\" => global::System.ServiceModel.TcpClientCredentialType.Windows,");
        builder.AppendLine("            _ => throw new global::System.ArgumentOutOfRangeException(nameof(value), $\"Unsupported credential type: {value}\")");
        builder.AppendLine("        };");
        if (options.OutputKind == GeneratedOutputKind.NetFramework48RestApiWrapper)
        {
            builder.AppendLine();
            builder.AppendLine("    private static global::System.ServiceModel.MessageCredentialType MapMessageCredentialType(string value)");
            builder.AppendLine("        => value switch");
            builder.AppendLine("        {");
            builder.AppendLine("            \"None\" => global::System.ServiceModel.MessageCredentialType.None,");
            builder.AppendLine("            \"Windows\" => global::System.ServiceModel.MessageCredentialType.Windows,");
            builder.AppendLine("            \"Certificate\" => global::System.ServiceModel.MessageCredentialType.Certificate,");
            builder.AppendLine("            \"UserName\" => global::System.ServiceModel.MessageCredentialType.UserName,");
            builder.AppendLine("            _ => throw new global::System.ArgumentOutOfRangeException(nameof(value), $\"Unsupported message credential type: {value}\")");
            builder.AppendLine("        };");
        }
        builder.AppendLine("}");

        return builder.ToString();
    }
}
