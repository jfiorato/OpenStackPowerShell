using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;

[Cmdlet(VerbsCommon.New, "OpenStackSession")]
public class NewOpenStackSession: PSCmdlet
{
    public NewOpenStackSession()
    {
        IdentityServiceEndpoint = Environment.GetEnvironmentVariable("OS_AUTH_URL");
        Region = Environment.GetEnvironmentVariable("OS_REGION_NAME");
        Username = Environment.GetEnvironmentVariable("OS_USERNAME");
        Password = Environment.GetEnvironmentVariable("OS_PASSWORD");
    }

    [Parameter]
    public string IdentityServiceEndpoint { get; set; }

    [Parameter]
    public string Region { get; set; }

    [Parameter]
    public string Username { get; set; }

    [Parameter]
    public string Password { get; set; }

    protected override void ProcessRecord()
    {
        HttpWebRequest request = (HttpWebRequest) WebRequest.Create(IdentityServiceEndpoint + "/tokens");
        request.Method = "POST";
        request.ContentType = "application/json";
        request.UserAgent = "OpenStack Powershell Tools";

        var dataString = String.Format("{{\"auth\":{{\"passwordCredentials\":{{\"username\":\"{0}\", \"password\":\"{1}\"}}}}}}", Username, Password);
        var data = Encoding.UTF8.GetBytes(dataString);
        request.ContentLength = data.Length;
        using (var requestStream = request.GetRequestStream())
        {
            requestStream.Write(data, 0, data.Length);
        }

        var response = request.GetResponse();

        string responseJson;

        using (var streamReader = new StreamReader(response.GetResponseStream()))
        {
            responseJson = streamReader.ReadToEnd();
        }

        var authData = JObject.Parse(responseJson);

        SessionState.PSVariable.Set(GetVariableName("Auth Token"), authData["access"]["token"].Value<string>("id"));

        WriteDebug(Region);

        var knownServices = new List<string>(new string[] {"compute","volume","object-store"});
        foreach (var service in authData["access"]["serviceCatalog"].Children())
        {
            if(!knownServices.Contains(service.Value<string>("type"))) continue;

            WriteDebug(service.Value<string>("type"));
            string endpoint = string.Empty;
            foreach (var serviceEndpoint in service["endpoints"].Children())
            {
                WriteDebug("  " + serviceEndpoint.Value<string>("region"));
                WriteDebug("  " + serviceEndpoint.Value<string>("publicURL"));
                if (serviceEndpoint.Value<string>("region") == String.Empty || serviceEndpoint.Value<string>("region") == Region)
                {
                    endpoint = serviceEndpoint.Value<string>("publicURL");
                    break;
                }
            }
            SessionState.PSVariable.Set(GetVariableName(service.Value<string>("type") + " Endpoint"), endpoint);
        }

        WriteDebug(String.Format("Succesfully authenticated as {0}", Username));
    }

    private string GetVariableName(string name)
    {
        name = name.Replace("-"," ");
        name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        name = name.Replace(" ", "");
        return "os" + name;
    }
}