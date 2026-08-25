namespace WcfNetTcpClientGenerator.Core;

/// <summary>Runs a WCF proxy-generation tool for the requested target framework.</summary>
public interface IWcfProxyToolRunner
{
    string ToolName { get; }
}
