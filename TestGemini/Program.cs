using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var apiKey = "YOUR_API_KEY_HERE";
        var filePath = "test_video.mp4"; // Create a dummy file

        // Create a small dummy file
        File.WriteAllBytes(filePath, new byte[1024]); // 1KB dummy

        using var client = new HttpClient();
        
        var requestUrl = $"https://generativelanguage.googleapis.com/upload/v1beta/files?uploadType=media&key={apiKey}";
        
        var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        
        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        // Sometimes Google APIs need X-Goog-Upload-File-Name, though the File URI will be returned
        request.Headers.Add("X-Goog-Upload-File-Name", "test_video.mp4");
        request.Content = fileContent;

        var response = await client.SendAsync(request);
        var responseString = await response.Content.ReadAsStringAsync();
        
        Console.WriteLine($"Status: {response.StatusCode}");
        Console.WriteLine($"Response: {responseString}");
    }
}
