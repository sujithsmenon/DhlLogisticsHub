namespace DhlLogistics.Mobile.Services;

using DhlLogistics.Shared.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

public class AuthService
{
    private readonly HttpClient _http;

    public AuthService(HttpClient http) => _http = http;

    public string? CurrentUserId   { get; private set; }
    public string? CurrentFullName { get; private set; }
    public string? CurrentRole     { get; private set; }
    public string? Token           { get; private set; }
    public bool    IsLoggedIn      => CurrentUserId is not null;

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email    = email,
                Password = password,
            });

            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result is null) return false;

            await SecureStorage.SetAsync("jwt_token",  result.Token);
            await SecureStorage.SetAsync("user_id",    result.UserId);
            await SecureStorage.SetAsync("user_name",  result.FullName);
            await SecureStorage.SetAsync("user_role",  result.Role);

            ApplyToken(result.Token, result.UserId, result.FullName, result.Role);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Call once on app start — restores session from secure storage
    public async Task<bool> TryRestoreSessionAsync()
    {
        var token    = await SecureStorage.GetAsync("jwt_token");
        var userId   = await SecureStorage.GetAsync("user_id");
        var fullName = await SecureStorage.GetAsync("user_name");
        var role     = await SecureStorage.GetAsync("user_role");

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
            return false;

        ApplyToken(token, userId, fullName ?? string.Empty, role ?? string.Empty);
        return true;
    }

    public async Task LogoutAsync()
    {
        SecureStorage.Remove("jwt_token");
        SecureStorage.Remove("user_id");
        SecureStorage.Remove("user_name");
        SecureStorage.Remove("user_role");

        _http.DefaultRequestHeaders.Authorization = null;
        Token           = null;
        CurrentUserId   = null;
        CurrentFullName = null;
        CurrentRole     = null;
    }

    private void ApplyToken(string token, string userId, string fullName, string role)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        Token           = token;
        CurrentUserId   = userId;
        CurrentFullName = fullName;
        CurrentRole     = role;
    }
}
