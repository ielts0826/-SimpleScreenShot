using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ScreenCaptureTool;

public static class ImgurUploader
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private const string ImgurApiEndpoint = "https://api.imgur.com/3/image";
    private const string ImgurTokenEndpoint = "https://api.imgur.com/oauth2/token";

    public static async Task<(string? AccessToken, string? RefreshToken, DateTime? ExpiresAt)> ExchangePinForTokensAsync(string clientId, string clientSecret, string pin)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(pin))
        {
            MainWindow.LogToFile("ImgurUploader.ExchangePinForTokensAsync: Client ID, Client Secret, or PIN is missing. Aborting token exchange.");
            return (null, null, null);
        }

        try
        {
            var requestParams = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "pin" },
                { "pin", pin }
            };

            using var content = new FormUrlEncodedContent(requestParams);
            
            MainWindow.LogToFile($"ImgurUploader.ExchangePinForTokensAsync: Attempting to exchange PIN for tokens. ClientID: {clientId.Substring(0, Math.Min(clientId.Length, 5))}...");

            var response = await HttpClient.PostAsync(ImgurTokenEndpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            MainWindow.LogToFile($"ImgurUploader.ExchangePinForTokensAsync: Token exchange response status: {response.StatusCode}, Content: {responseContent}");

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonDocument.Parse(responseContent);
                string? accessToken = null;
                string? refreshToken = null;
                int expiresIn = 0;

                if (jsonResponse.RootElement.TryGetProperty("access_token", out var accessTokenElement))
                {
                    accessToken = accessTokenElement.GetString();
                }
                if (jsonResponse.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement))
                {
                    refreshToken = refreshTokenElement.GetString();
                }
                if (jsonResponse.RootElement.TryGetProperty("expires_in", out var expiresInElement) && expiresInElement.TryGetInt32(out int expVal))
                {
                    expiresIn = expVal;
                }

                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(refreshToken) && expiresIn > 0)
                {
                    DateTime expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
                    MainWindow.LogToFile($"ImgurUploader.ExchangePinForTokensAsync: Successfully obtained tokens. AccessToken (partial): {accessToken.Substring(0, Math.Min(accessToken.Length,8))}..., RefreshToken (partial): {refreshToken.Substring(0,Math.Min(refreshToken.Length,8))}..., ExpiresAt: {expiresAt}");
                    return (accessToken, refreshToken, expiresAt);
                }
                else
                {
                    MainWindow.LogToFile("ImgurUploader.ExchangePinForTokensAsync: Token exchange succeeded but some token fields are missing in the response.");
                    return (null, null, null);
                }
            }
            else
            {
                // Log detailed error from Imgur if possible
                try
                {
                    var errorResponse = JsonDocument.Parse(responseContent);
                    if (errorResponse.RootElement.TryGetProperty("error", out var errorElement)) // Imgur often returns 'error' or 'data.error'
                    {
                         MainWindow.LogToFile($"ImgurUploader.ExchangePinForTokensAsync: API Error from token endpoint: {errorElement.GetString()}");
                    }
                    else if (errorResponse.RootElement.TryGetProperty("data", out var dataElement) && dataElement.TryGetProperty("error", out var nestedErrorElement))
                    {
                        MainWindow.LogToFile($"ImgurUploader.ExchangePinForTokensAsync: API Error from token endpoint: {nestedErrorElement.GetString()}");
                    }
                    else
                    {
                        MainWindow.LogToFile($"ImgurUploader.ExchangePinForTokensAsync: API Error from token endpoint: {response.ReasonPhrase} (Raw: {responseContent})");
                    }
                }
                catch { /* Parsing error JSON failed, already logged raw content */ }
                return (null, null, null);
            }
        }
        catch (Exception ex)
        {
            MainWindow.LogToFile($"ImgurUploader.ExchangePinForTokensAsync: Exception during token exchange: {ex.ToString()}");
            return (null, null, null);
        }
    }

    // Upload methods now accept an optional accessToken for authenticated uploads
    public static async Task<string?> UploadImageAsync(byte[] imageBytes, string clientId, string? accessToken = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            MainWindow.LogToFile("ImgurUploader.UploadImageAsync: Client ID is missing. Upload aborted.");
            return null;
        }
        // If accessToken is provided, ClientID is still useful for logging or if Imgur API requires it for some non-auth header context, but auth will be Bearer.

        try
        {
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            content.Add(imageContent, "image");

            var request = new HttpRequestMessage(HttpMethod.Post, ImgurApiEndpoint);

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                MainWindow.LogToFile($"ImgurUploader.UploadImageAsync: Attempting authenticated upload with Access Token. Bytes: {imageBytes.Length}");
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", clientId);
                MainWindow.LogToFile($"ImgurUploader.UploadImageAsync: Attempting anonymous upload with Client-ID. Bytes: {imageBytes.Length}");
            }
            
            request.Content = content;

            // MainWindow.LogToFile($"ImgurUploader: Attempting to upload {imageBytes.Length} bytes."); // Covered by specific log above

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

    // but ensure `imageContent.Headers.ContentType` is "image/gif"
    public static async Task<string?> UploadGifAsync(byte[] gifBytes, string clientId, string? accessToken = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            MainWindow.LogToFile("ImgurUploader.UploadGifAsync: Client ID is missing. Upload aborted.");
            return null;
        }

         try
        {
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(gifBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/gif"); 

            content.Add(imageContent, "image");

            var request = new HttpRequestMessage(HttpMethod.Post, ImgurApiEndpoint);

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                MainWindow.LogToFile($"ImgurUploader.UploadGifAsync: Attempting authenticated GIF upload with Access Token. Bytes: {gifBytes.Length}");
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", clientId);
                MainWindow.LogToFile($"ImgurUploader.UploadGifAsync: Attempting anonymous GIF upload with Client-ID. Bytes: {gifBytes.Length}");
            }
            
            request.Content = content;

            // MainWindow.LogToFile($"ImgurUploader: Attempting to upload GIF {gifBytes.Length} bytes."); // Covered by specific log above
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