using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScreenCaptureTool;

public static class ImgurUploader
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private const string ImgurApiEndpoint = "https://api.imgur.com/3/image";

    // Annoymous upload does not require OAuth token, only Client-ID
    public static async Task<string?> UploadImageAsync(byte[] imageBytes, string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId) || clientId == "YOUR_IMGUR_CLIENT_ID_PLACEHOLDER")
        {
            MainWindow.LogToFile("ImgurUploader: Client ID provided is missing, invalid, or a placeholder. Upload aborted.");
            // Optionally, communicate this to the user more directly.
            return null;
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png"); // Assuming PNG, adjust if other formats are used

            content.Add(imageContent, "image"); // "image" is the required name for the field by Imgur API

            var request = new HttpRequestMessage(HttpMethod.Post, ImgurApiEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", clientId);
            request.Content = content;

            MainWindow.LogToFile($"ImgurUploader: Attempting to upload {imageBytes.Length} bytes.");

            var response = await HttpClient.SendAsync(request);

            var responseContent = await response.Content.ReadAsStringAsync();
            MainWindow.LogToFile($"ImgurUploader: Response status: {response.StatusCode}, Content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonDocument.Parse(responseContent);
                if (jsonResponse.RootElement.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("link", out var linkElement))
                {
                    return linkElement.GetString();
                }
                MainWindow.LogToFile("ImgurUploader: Upload succeeded but link not found in response.");
                return null;
            }
            else
            {
                // Try to get error message from Imgur response
                try 
                {
                    var errorResponse = JsonDocument.Parse(responseContent);
                    if (errorResponse.RootElement.TryGetProperty("data", out var dataElement) && 
                        dataElement.TryGetProperty("error", out var errorElement))
                    {
                        MainWindow.LogToFile($"ImgurUploader: API Error: {errorElement.GetString()}");
                    } else {
                        MainWindow.LogToFile($"ImgurUploader: API Error: {response.ReasonPhrase} (Raw: {responseContent})");
                    }
                }
                catch
                {
                     MainWindow.LogToFile($"ImgurUploader: API Error: {response.ReasonPhrase} (Raw content not valid JSON: {responseContent})");
                }
                return null;
            }
        }
        catch (Exception ex)
        {
            MainWindow.LogToFile($"ImgurUploader: Exception during upload: {ex.ToString()}");
            return null;
        }
    }

    // Placeholder for potential GIF upload - Imgur API for anonymous GIF upload is same as image
    // but ensure `imageContent.Headers.ContentType` is "image/gif"
    public static async Task<string?> UploadGifAsync(byte[] gifBytes, string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId) || clientId == "YOUR_IMGUR_CLIENT_ID_PLACEHOLDER")
        {
            MainWindow.LogToFile("ImgurUploader: Client ID provided for GIF upload is missing, invalid, or a placeholder. Upload aborted.");
            return null;
        }
         try
        {
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(gifBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/gif"); 

            content.Add(imageContent, "image");

            var request = new HttpRequestMessage(HttpMethod.Post, ImgurApiEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", clientId);
            request.Content = content;

            MainWindow.LogToFile($"ImgurUploader: Attempting to upload GIF {gifBytes.Length} bytes.");
            var response = await HttpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            MainWindow.LogToFile($"ImgurUploader: GIF Upload Response status: {response.StatusCode}, Content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonDocument.Parse(responseContent);
                if (jsonResponse.RootElement.TryGetProperty("data", out var dataElement) &&
                    dataElement.TryGetProperty("link", out var linkElement))
                {
                    return linkElement.GetString();
                }
                return null;
            }
            return null;
        }
        catch (Exception ex)
        {
            MainWindow.LogToFile($"ImgurUploader: Exception during GIF upload: {ex.ToString()}");
            return null;
        }
    }
} 