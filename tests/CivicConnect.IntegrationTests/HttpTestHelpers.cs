using Xunit;
using System.Net;
using System.Text.RegularExpressions;

namespace CivicConnect.IntegrationTests;

public static class HttpTestHelpers
{
    public static async Task<string> TokenAsync(HttpClient client, string path)
    {
        var page = await client.GetAsync(path);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            match = Regex.Match(html, "value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"");
        Assert.True(match.Success, "Antiforgery token not rendered at " + path);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path,
        string getFormPath, Dictionary<string, string> values)
    {
        var token = await TokenAsync(client, getFormPath);
        values["__RequestVerificationToken"] = token;
        return await client.PostAsync(path, new FormUrlEncodedContent(values));
    }

    public static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await PostAsync(client, "/Account/Login", "/Account/Login", new()
        {
            ["Email"] = email, ["Password"] = "TestPassword99!"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
