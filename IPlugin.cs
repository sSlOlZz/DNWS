using System.Collections.Generic;

namespace DNWS
{
    public interface IPlugin
    {
        void PreProcessing(HTTPRequest request);
        HTTPResponse PostProcessing(HTTPResponse response);
        HTTPResponse GetResponse(HTTPRequest request);
    }

    public interface IPluginWithParameters : IPlugin
    {
        void SetParameters(Dictionary<string, string> parameters);
    }

}