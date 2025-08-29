using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class Script : ScriptBase
{
    public override async Task<HttpResponseMessage> ExecuteAsync()
    {
        // Call the backend API
        var response = await this.Context.SendAsync(this.Context.Request, this.CancellationToken);

        // ✅ If response is already OK (2xx), inspect only for domain/url lookups
        if (response.IsSuccessStatusCode)
        {
            string path = this.Context.Request.RequestUri.AbsolutePath.ToLower();

            // Apply special rule only for /v4/domain and /v4/url endpoints
            if (path.Contains("/v4/domain") || path.Contains("/v4/url") || path.Contains("/v4/ip"))
            {
                var content = await response.Content.ReadAsStringAsync();

                try
                {
                    var json = JObject.Parse(content);
                    var detectedBy = (int?)json["lookup_results"]?["detected_by"] ?? -1;

                    if (detectedBy == 0)
                    {
                        // Return clean "no findings" message
                        var noFindings = new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                "{ \"message\": \"No findings.\" }",
                                Encoding.UTF8,
                                "application/json")
                        };
                        return noFindings;
                    }
                }
                catch
                {
                    // If parsing fails, just return original response
                    return response;
                }
            }

            // For other paths, or detected_by > 0 → return response as-is
            return response;
        }

        // ❌ Otherwise, handle errors
        string message;
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound: // 404
                message = "{\"message\": \"Not Found - The requested resource does not exist.\"}";
                break;

            case HttpStatusCode.BadRequest: // 400
                message = "{\"message\": \"Invalid Input - Please check your request parameters.\"}";
                break;

            case HttpStatusCode.InternalServerError: // 500
                message = "{\"message\": \"Internal Error - Something went wrong on the server side.\"}";
                break;

            default: // Any other error code
                message = "{\"message\": \"Unhandled error - Backend returned status code " 
                          + (int)response.StatusCode + ".\"}";
                break;
        }

        // ✅ Always return 200 OK with custom error message
        var newResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(message, Encoding.UTF8, "application/json")
        };

        return newResponse;
    }
}
