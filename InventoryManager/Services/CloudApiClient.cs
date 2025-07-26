// Services/CloudApiClient.cs
// Implementation of the cloud API client for synchronization

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using InventoryManager.Models;
using Microsoft.Extensions.Logging;

namespace InventoryManager.Services
{
    /// <summary>
    /// Interface for cloud API operations
    /// </summary>
    public interface ICloudApiClient
    {
        Task<bool> IsConnectedAsync();
        Task<AuthResponse?> LoginAsync(string username, string password);
        Task<AuthResponse?> RefreshTokenAsync(string refreshToken);
        Task<bool> RegisterDeviceAsync(DeviceRegistration device);

        // Sync operations
        Task<SyncChangesResponse?> GetChangesAsync(DateTime since, string? tableFilter = null);
        Task<SyncPushResponse?> PushChangesAsync(List<SyncChange> changes);
        Task<ConflictResolutionResult?> ResolveConflictAsync(int conflictId, ConflictResolutionStrategy strategy, object resolvedData);
        Task<SyncStatusResponse?> GetSyncStatusAsync();

        // Data operations
        Task<List<InventoryItem>?> GetInventoryItemsAsync(DateTime? lastModified = null);
        Task<InventoryItem?> CreateInventoryItemAsync(InventoryItem item);
        Task<InventoryItem?> UpdateInventoryItemAsync(int id, InventoryItem item);
        Task<bool> DeleteInventoryItemAsync(int id);
        Task<List<InventoryTransaction>?> GetTransactionsAsync(DateTime since);
        Task<bool> BatchCreateTransactionsAsync(List<InventoryTransaction> transactions);
    }

    /// <summary>
    /// Cloud API client implementation
    /// </summary>
    public class CloudApiClient : ICloudApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CloudApiClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private string? _accessToken;
        private string? _refreshToken;
        private DateTime? _tokenExpiry;

        // Configuration
        private readonly string _baseUrl;
        private readonly TimeSpan _httpTimeout = TimeSpan.FromSeconds(30);

        public CloudApiClient(HttpClient httpClient, ILogger<CloudApiClient> logger, string baseUrl)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = baseUrl;

            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.Timeout = _httpTimeout;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Check if we can connect to the cloud service
        /// </summary>
        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/v1/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to cloud service");
                return false;
            }
        }

        /// <summary>
        /// Authenticate with the cloud service
        /// </summary>
        public async Task<AuthResponse?> LoginAsync(string username, string password)
        {
            try
            {
                var request = new
                {
                    username,
                    password,
                    deviceId = GetDeviceId()
                };

                var response = await PostAsync<AuthResponse>("/api/v1/auth/login", request);

                if (response != null)
                {
                    _accessToken = response.AccessToken;
                    _refreshToken = response.RefreshToken;
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);
                    SetAuthHeader();
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed");
                return null;
            }
        }

        /// <summary>
        /// Refresh authentication token
        /// </summary>
        public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var request = new { refreshToken };
                var response = await PostAsync<AuthResponse>("/api/v1/auth/refresh", request);

                if (response != null)
                {
                    _accessToken = response.AccessToken;
                    _refreshToken = response.RefreshToken;
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);
                    SetAuthHeader();
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh failed");
                return null;
            }
        }

        /// <summary>
        /// Register device with cloud service
        /// </summary>
        public async Task<bool> RegisterDeviceAsync(DeviceRegistration device)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var response = await PostAsync("/api/v1/auth/register-device", device);
                return response != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Device registration failed");
                return false;
            }
        }

        /// <summary>
        /// Get changes from cloud since last sync
        /// </summary>
        public async Task<SyncChangesResponse?> GetChangesAsync(DateTime since, string? tableFilter = null)
        {
            try
            {
                await EnsureAuthenticatedAsync();

                var query = $"?since={since:O}";
                if (!string.IsNullOrEmpty(tableFilter))
                    query += $"&table={tableFilter}";

                return await GetAsync<SyncChangesResponse>($"/api/v1/sync/changes{query}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get sync changes");
                return null;
            }
        }

        /// <summary>
        /// Push local changes to cloud
        /// </summary>
        public async Task<SyncPushResponse?> PushChangesAsync(List<SyncChange> changes)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                return await PostAsync<SyncPushResponse>("/api/v1/sync/changes", changes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push sync changes");
                return null;
            }
        }

        /// <summary>
        /// Resolve a sync conflict
        /// </summary>
        public async Task<ConflictResolutionResult?> ResolveConflictAsync(int conflictId, ConflictResolutionStrategy strategy, object resolvedData)
        {
            try
            {
                await EnsureAuthenticatedAsync();

                var request = new
                {
                    strategy = strategy.ToString(),
                    resolvedData
                };

                return await PostAsync<ConflictResolutionResult>($"/api/v1/sync/conflicts/{conflictId}/resolve", request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve conflict");
                return null;
            }
        }

        /// <summary>
        /// Get current sync status
        /// </summary>
        public async Task<SyncStatusResponse?> GetSyncStatusAsync()
        {
            try
            {
                await EnsureAuthenticatedAsync();
                return await GetAsync<SyncStatusResponse>("/api/v1/sync/status");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get sync status");
                return null;
            }
        }

        /// <summary>
        /// Get inventory items from cloud
        /// </summary>
        public async Task<List<InventoryItem>?> GetInventoryItemsAsync(DateTime? lastModified = null)
        {
            try
            {
                await EnsureAuthenticatedAsync();

                var query = lastModified.HasValue ? $"?lastModified={lastModified.Value:O}" : "";
                return await GetAsync<List<InventoryItem>>($"/api/v1/inventory/items{query}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get inventory items");
                return null;
            }
        }

        /// <summary>
        /// Create inventory item in cloud
        /// </summary>
        public async Task<InventoryItem?> CreateInventoryItemAsync(InventoryItem item)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                return await PostAsync<InventoryItem>("/api/v1/inventory/items", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create inventory item");
                return null;
            }
        }

        /// <summary>
        /// Update inventory item in cloud
        /// </summary>
        public async Task<InventoryItem?> UpdateInventoryItemAsync(int id, InventoryItem item)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                return await PutAsync<InventoryItem>($"/api/v1/inventory/items/{id}", item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update inventory item");
                return null;
            }
        }

        /// <summary>
        /// Delete inventory item from cloud
        /// </summary>
        public async Task<bool> DeleteInventoryItemAsync(int id)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                return await DeleteAsync($"/api/v1/inventory/items/{id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete inventory item");
                return false;
            }
        }

        /// <summary>
        /// Get transactions from cloud
        /// </summary>
        public async Task<List<InventoryTransaction>?> GetTransactionsAsync(DateTime since)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                return await GetAsync<List<InventoryTransaction>>($"/api/v1/inventory/transactions?since={since:O}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transactions");
                return null;
            }
        }

        /// <summary>
        /// Batch create transactions in cloud
        /// </summary>
        public async Task<bool> BatchCreateTransactionsAsync(List<InventoryTransaction> transactions)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var response = await PostAsync("/api/v1/inventory/transactions/batch", transactions);
                return response != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to batch create transactions");
                return false;
            }
        }

        #region Helper Methods

        private async Task EnsureAuthenticatedAsync()
        {
            if (string.IsNullOrEmpty(_accessToken) || _tokenExpiry == null)
            {
                throw new InvalidOperationException("Not authenticated");
            }

            // Refresh token if it's about to expire (5 minutes buffer)
            if (_tokenExpiry.Value.Subtract(DateTime.UtcNow).TotalMinutes < 5)
            {
                if (!string.IsNullOrEmpty(_refreshToken))
                {
                    await RefreshTokenAsync(_refreshToken);
                }
                else
                {
                    throw new InvalidOperationException("Token expired and no refresh token available");
                }
            }
        }

        private void SetAuthHeader()
        {
            if (!string.IsNullOrEmpty(_accessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _accessToken);
            }
        }

        private async Task<T?> GetAsync<T>(string endpoint)
        {
            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"GET {endpoint} failed with status {response.StatusCode}");
                return default;
            }

            var json = await response.Content.ReadAsStringAsync();
            var envelope = JsonSerializer.Deserialize<ApiResponseEnvelope<T>>(json, _jsonOptions);

            return envelope?.Success == true ? envelope.Data : default;
        }

        private async Task<T?> PostAsync<T>(string endpoint, object data)
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"POST {endpoint} failed with status {response.StatusCode}");
                return default;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var envelope = JsonSerializer.Deserialize<ApiResponseEnvelope<T>>(responseJson, _jsonOptions);

            return envelope?.Success == true ? envelope.Data : default;
        }

        private async Task<object?> PostAsync(string endpoint, object data)
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            return response.IsSuccessStatusCode ? new object() : null;
        }

        private async Task<T?> PutAsync<T>(string endpoint, object data)
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"PUT {endpoint} failed with status {response.StatusCode}");
                return default;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var envelope = JsonSerializer.Deserialize<ApiResponseEnvelope<T>>(responseJson, _jsonOptions);

            return envelope?.Success == true ? envelope.Data : default;
        }

        private async Task<bool> DeleteAsync(string endpoint)
        {
            var response = await _httpClient.DeleteAsync(endpoint);
            return response.IsSuccessStatusCode;
        }

        private string GetDeviceId()
        {
            // In a real implementation, this would return a unique device identifier
            return Microsoft.Maui.Devices.DeviceInfo.Name ?? "Unknown Device";
        }

        #endregion
    }

    #region Response Models

    public class ApiResponseEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public ApiMetadata? Metadata { get; set; }
        public List<string>? Errors { get; set; }
    }

    public class ApiMetadata
    {
        public DateTime Timestamp { get; set; }
        public string Version { get; set; } = "1.0";
        public PaginationInfo? Pagination { get; set; }
    }

    public class PaginationInfo
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = "Bearer";
    }

    public class SyncChangesResponse
    {
        public List<SyncChange> Changes { get; set; } = new();
        public List<SyncConflict> Conflicts { get; set; } = new();
        public List<SyncDeletion> Deletions { get; set; } = new();
        public bool HasMore { get; set; }
        public string? NextToken { get; set; }
    }

    public class SyncChange
    {
        public string Table { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public object Data { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string ETag { get; set; } = string.Empty;
    }

    public class SyncDeletion
    {
        public string Table { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; }
    }

    public class SyncPushResponse
    {
        public int Accepted { get; set; }
        public int Rejected { get; set; }
        public List<SyncError> Errors { get; set; } = new();
    }

    public class SyncError
    {
        public string EntityId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class SyncStatusResponse
    {
        public DateTime LastSyncAt { get; set; }
        public int PendingChanges { get; set; }
        public int Conflicts { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    #endregion
}