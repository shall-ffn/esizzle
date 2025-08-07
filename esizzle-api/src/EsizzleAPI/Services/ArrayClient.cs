using EsizzleAPI.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using System.Net.Http.Headers;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using EsizzleAPI.Repositories;
using SystemTextJson = System.Text.Json.JsonSerializer;

namespace EsizzleAPI.Services;

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; } = 3600;
    
    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}

public class ArrayClient : IArrayClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArrayClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDbConnectionFactory _dbConnectionFactory;
    
    // Cache for token to avoid requesting a new one for every API call
    private string _accessToken = string.Empty;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    
    // Legacy path configurations
    private readonly string _arrayApiBaseAddress;
    private readonly string _tokenPath;
    private readonly string _controllerPath;
    private readonly string _userIntegrationPath;
    private readonly string _projectPatchPath;
    private readonly string _projectPendingDeletePath;
    private readonly string _projectDeletePath;
    private readonly string _objectPendingDeletePath;
    private readonly string _finalizeObjectDeleteRequestPath;

    public ArrayClient(
        HttpClient httpClient, 
        ILogger<ArrayClient> logger, 
        IConfiguration configuration,
        IDbConnectionFactory dbConnectionFactory)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _dbConnectionFactory = dbConnectionFactory;
        
        // Initialize legacy path configurations
        _arrayApiBaseAddress = configuration["ArrayAPI:BaseUrl"];
        var tokenBaseUrl = configuration["ArrayToken:BaseUrl"];
        
        // Log configuration values for debugging
        _logger.LogInformation("ArrayAPI:BaseUrl configured as: {BaseUrl}", _arrayApiBaseAddress);
        _logger.LogInformation("ArrayToken:BaseUrl configured as: {TokenBaseUrl}", tokenBaseUrl);
        _logger.LogInformation("ArrayAPI:UseMockData configured as: {UseMockData}", configuration.GetValue<bool>("ArrayAPI:UseMockData", true));
        
        _tokenPath = configuration["ArrayAPI:TokenPath"] ?? "/token";
        _controllerPath = configuration["ArrayAPI:ControllerPath"] ?? "/prod/api/v0/flightdeck/";
        _userIntegrationPath = configuration["ArrayAPI:UserIntegrationPath"] ?? "/prod/api/v0/flightdeck/user/{0}";
        _projectPatchPath = configuration["ArrayAPI:ProjectPatchPath"] ?? "/prod/api/v0/flightdeck/project/{0}";
        _projectPendingDeletePath = configuration["ArrayAPI:ProjectPendingDeletePath"] ?? "/prod/api/v0/flightdeck/project/pendingDelete";
        _projectDeletePath = configuration["ArrayAPI:ProjectDeletePath"] ?? "/prod/api/v0/flightdeck/project/{0}";
        _objectPendingDeletePath = configuration["ArrayAPI:ObjectsPendingDeletePath"] ?? "/prod/api/v0/flightdeck/objects/pendingDelete";
        _finalizeObjectDeleteRequestPath = configuration["ArrayAPI:FinalizeDeleteRequestPath"] ?? "/prod/api/v0/flightdeck/objects/pendingDelete/{0}";
        
        // Initialize with a valid token
        RefreshTokenIfStale();
    }

    /// <summary>
    /// Gets an access token from the Array token endpoint
    /// </summary>
    private void RefreshTokenIfStale()
    {
        // If the token's refresh time hasn't been set or if it's been 12 hours since it was, then refresh it
        if (_tokenExpiresAt == DateTime.MinValue || _tokenExpiresAt < DateTime.UtcNow)
        {                
            try
            {
                var token = GetToken();
                _accessToken = token.AccessToken;
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
                _logger.LogInformation("Successfully refreshed access token, expires at {ExpiresAt}", _tokenExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
            }
        }
    }
    
    private Token GetToken()
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData)
        {
            _logger.LogInformation("Using mock token data");
            return new Token
            {
                AccessToken = "mock-access-token-for-array-api",
                TokenType = "Bearer",
                ExpiresIn = 3600
            };
        }
        
        using var client = new HttpClient();
        client.BaseAddress = new Uri(_arrayApiBaseAddress);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username", _configuration["ArrayAPI:ServiceAccountUserName"]),
            new KeyValuePair<string, string>("password", _configuration["ArrayAPI:ServiceAccountPassword"])
        });

        var tokenResponse = ValidatedResponse(client.PostAsync(_tokenPath, content).Result);
        var tokenJson = tokenResponse.Content.ReadAsStringAsync().Result;
        var accessToken = JsonConvert.DeserializeObject<Token>(tokenJson);

        return accessToken ?? new Token();
    }
    
    private HttpResponseMessage ValidatedResponse(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                throw new ConnectivityException("Cannot connect to Array API");
            case HttpStatusCode.InternalServerError:
                throw new ServerErrorException("Server error in Array API");
            default:
                return response;
        }
    }
    
    private HttpClient GetHttpClient()
    {
        RefreshTokenIfStale();
        
        var client = new HttpClient();
        client.BaseAddress = new Uri(_arrayApiBaseAddress);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        return client;
    }
    
    private async Task<string> GetAccessTokenAsync()
    {
        // Return cached token if it's still valid (with 5 minute buffer)
        if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            _logger.LogDebug("Using cached access token that expires at {ExpiresAt}", _tokenExpiresAt);
            return _accessToken;
        }
        
        try
        {
            var tokenBaseUrl = _configuration["ArrayToken:BaseUrl"];
            var tokenEndpoint = _configuration["ArrayToken:Endpoint"] ?? "/token";
            var useMockData = _configuration.GetValue<bool>("ArrayToken:UseMockData", true);
            var timeout = _configuration.GetValue<int>("ArrayToken:Timeout", 30000);
            
            if (useMockData || string.IsNullOrEmpty(tokenBaseUrl))
            {
                _logger.LogInformation("Using mock token data");
                _accessToken = "mock-access-token-for-array-api";
                _tokenExpiresAt = DateTime.UtcNow.AddHours(1);
                return _accessToken;
            }
            
            _logger.LogInformation("Requesting token from {BaseUrl}{Endpoint}", tokenBaseUrl, tokenEndpoint);
            
            // Create a separate HttpClient for token requests with appropriate timeout
            using var tokenClient = new HttpClient
            {
                BaseAddress = new Uri(tokenBaseUrl),
                Timeout = TimeSpan.FromMilliseconds(timeout)
            };
            
            // Add any necessary headers for token request
            tokenClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // Get credentials from configuration
            var username = _configuration["ArrayToken:Username"] ?? "system@arraytechnology.com";
            var password = _configuration["ArrayToken:Password"] ?? "default-password";
            
            _logger.LogInformation("Using credentials for user: {Username}", username);
            
            // Prepare token request with x-www-form-urlencoded format as required - exactly matching Postman
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            });
            
            // Create a request message directly for more control over headers and content
            var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            
            // Set content-type header at both request and content levels
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // Use StringContent directly for guaranteed control over Content-Type
            string formData = $"grant_type=password&username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}";
            var content = new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Content = content;
            
            // Add debugging to see what's being sent
            _logger.LogInformation("Sending token request to {BaseUrl}{Endpoint} with parameters: grant_type=password, username={Username}, content-type={ContentType}", 
                tokenClient.BaseAddress, tokenEndpoint, username, content.Headers.ContentType);
            
            var tokenResponse = await tokenClient.SendAsync(request);
            
            // Read the response content regardless of status code for logging
            var responseContent = await tokenResponse.Content.ReadAsStringAsync();
            
            if (tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Token request successful, response: {Response}", responseContent);
                var tokenData = SystemTextJson.Deserialize<TokenResponse>(responseContent);
                
                if (tokenData != null)
                {
                    _accessToken = tokenData.AccessToken;
                    _tokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                    _logger.LogInformation("Successfully retrieved access token, expires in {ExpiresIn} seconds", tokenData.ExpiresIn);
                    return _accessToken;
                }
            }
            else
            {
                _logger.LogError("Token request failed. Status: {StatusCode}, Response: {Response}", 
                    tokenResponse.StatusCode, responseContent);
            }
            
            _logger.LogError("Failed to retrieve access token. Status: {StatusCode}", tokenResponse.StatusCode);
            throw new Exception($"Failed to retrieve access token: {tokenResponse.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting access token");
            throw;
        }
    }
    
    /// <summary>
    /// Makes an authenticated request to the Array API
    /// </summary>
    private async Task<T?> MakeAuthenticatedRequestAsync<T>(string endpoint, HttpMethod method, object? data = null)
    {
        try
        {
            var arrayApiUrl = _configuration["ArrayAPI:BaseUrl"];
            var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
            var timeout = _configuration.GetValue<int>("ArrayAPI:Timeout", 30000);
            
            if (string.IsNullOrEmpty(arrayApiUrl))
            {
                throw new ArgumentException("ArrayAPI:BaseUrl is not configured");
            }
            
            // Get access token if making a real API call
            if (!useMockData)
            {
                var token = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            
            // Set timeout from configuration
            _httpClient.Timeout = TimeSpan.FromMilliseconds(timeout);
            
            // Create the request
            var requestUri = $"{arrayApiUrl}{endpoint}";
            HttpResponseMessage response;
            
            if (method == HttpMethod.Get)
            {
                response = await _httpClient.GetAsync(requestUri);
            }
            else if (method == HttpMethod.Post)
            {
                var content = new StringContent(
                    SystemTextJson.Serialize(data),
                    Encoding.UTF8,
                    new MediaTypeHeaderValue("application/json")
                );
                response = await _httpClient.PostAsync(requestUri, content);
            }
            else
            {
                throw new NotImplementedException($"HTTP method {method} not implemented");
            }
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                return SystemTextJson.Deserialize<T>(jsonContent);
            }
            
            _logger.LogWarning("API call failed: {StatusCode} - {Endpoint}", response.StatusCode, endpoint);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error making authenticated request to {Endpoint}", endpoint);
            return default;
        }
    }

    public async Task<List<SystemRole>?> GetUserSystemRolesAsync(string userEmail)
    {
        try
        {
            var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
            
            _logger.LogInformation("GetUserSystemRolesAsync called for {Email}. UseMockData: {UseMockData}, BaseAddress: {BaseAddress}", 
                userEmail, useMockData, _arrayApiBaseAddress);
            
            if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
            {
                _logger.LogInformation("Using database fallback for user system roles: {Email}", userEmail);
                return await GetUserSystemRolesFromDatabaseAsync(userEmail);
            }
            
            // Use the configured UserIntegrationPath instead of hardcoded endpoint
            var endpoint = string.Format(_userIntegrationPath, Uri.EscapeDataString(userEmail));
            _logger.LogInformation("Making API call to endpoint: {Endpoint} for user {Email}", endpoint, userEmail);
            
            // Get the full user integration data and extract system roles
            var userData = await MakeAuthenticatedRequestAsync<UserIntegrationModel>(endpoint, HttpMethod.Get);
            
            if (userData?.SystemRoles != null)
            {
                _logger.LogInformation("API call returned {Count} system roles for user {Email}: {Roles}", 
                    userData.SystemRoles.Count, userEmail, string.Join(", ", userData.SystemRoles));
                
                // Convert from Models.SystemRoleEnum to Services.SystemRole
                return userData.SystemRoles.Select(role => (SystemRole)Enum.ToObject(typeof(SystemRole), (int)role)).ToList();
            }
            
            // Fallback to database if API call failed or returned null
            _logger.LogWarning("ArrayAPI call failed or returned null for {Email}", userEmail);
            return await GetUserSystemRolesFromDatabaseAsync(userEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling ArrayAPI for user {Email}", userEmail);
            // Fallback to database
            return await GetUserSystemRolesFromDatabaseAsync(userEmail);
        }
    }

    public async Task<bool> IsUserAdministratorAsync(string userEmail)
    {
        var roles = await GetUserSystemRolesAsync(userEmail);
        return roles?.Contains(SystemRole.Administrator) ?? false;
    }
    
    /// <summary>
    /// Get user system roles (sync version for legacy compatibility)
    /// </summary>
    public IList<SystemRoleEnum> GetUserSystemRoles(string email)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for GetUserSystemRoles");
            return new List<SystemRoleEnum> { SystemRoleEnum.Administrator };
        }
        
        try
        {
            var url = string.Format(_userIntegrationPath, Uri.EscapeDataString(email));
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.GetAsync(url).Result);
            var content = response.Content.ReadAsStringAsync().Result;
            var userData = JsonConvert.DeserializeObject<UserIntegrationModel>(content);
            
            // Convert from Models.SystemRoleEnum to Services.SystemRoleEnum
            if (userData?.SystemRoles != null)
            {
                return userData.SystemRoles.Select(role => (SystemRoleEnum)Enum.ToObject(typeof(SystemRoleEnum), (int)role)).ToList();
            }
            
            return new List<SystemRoleEnum>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user system roles for {Email}", email);
            return new List<SystemRoleEnum>();
        }
    }
    
    /// <summary>
    /// Update project metrics
    /// </summary>
    public bool UpdateProjectMetrics(int projectId, ProjectUpdateModel update)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for UpdateProjectMetrics");
            return true;
        }
        
        try
        {
            var url = string.Format(_projectPatchPath, projectId);
            using var client = GetHttpClient();
            
            var jsonContent = JsonConvert.SerializeObject(update);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = ValidatedResponse(client.PatchAsync(url, content).Result);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project metrics for project {ProjectId}", projectId);
            return false;
        }
    }
    
    /// <summary>
    /// Get projects pending deletion
    /// </summary>
    public IList<ProjectModel> GetProjectsPendingDelete()
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for GetProjectsPendingDelete");
            return new List<ProjectModel>
            {
                new ProjectModel
                {
                    Id = 1,
                    Name = "Mock Project Pending Delete",
                    Description = "This is a mock project",
                    Status = "Pending Delete"
                }
            };
        }
        
        try
        {
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.GetAsync(_projectPendingDeletePath).Result);
            var content = response.Content.ReadAsStringAsync().Result;
            var projects = JsonConvert.DeserializeObject<List<ProjectModel>>(content);
            
            return projects ?? new List<ProjectModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects pending delete");
            return new List<ProjectModel>();
        }
    }
    
    /// <summary>
    /// Get all projects
    /// </summary>
    public IList<ProjectModel> GetProjects()
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for GetProjects");
            return new List<ProjectModel>
            {
                new ProjectModel
                {
                    Id = 1,
                    Name = "Mock Project 1",
                    Description = "This is a mock project",
                    Status = "Active"
                },
                new ProjectModel
                {
                    Id = 2,
                    Name = "Mock Project 2",
                    Description = "This is another mock project",
                    Status = "Active"
                }
            };
        }
        
        try
        {
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.GetAsync($"{_controllerPath}projects").Result);
            var content = response.Content.ReadAsStringAsync().Result;
            var projects = JsonConvert.DeserializeObject<List<ProjectModel>>(content);
            
            return projects ?? new List<ProjectModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects");
            return new List<ProjectModel>();
        }
    }

    /// <summary>
    /// Get roles for a user on a specific project
    /// </summary>
    public IList<string> GetUserProjectRole(string email, int projectId)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for GetUserProjectRole");
            return new List<string> { "Project Admin", "Project Member" };
        }
        
        try
        {
            var userData = GetUserIntegrationData(email);
            var project = userData.ProjectRoles.FirstOrDefault(p => p.ProjectId == projectId);
            return project?.Roles ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user project role for {Email} on project {ProjectId}", email, projectId);
            return new List<string>();
        }
    }
    
    /// <summary>
    /// Set roles for a user on a specific project
    /// </summary>
    public UserProjectRoleModel SetUserProjectRoles(string email, int projectId, UserProjectRoleModel data)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for SetUserProjectRoles");
            return new UserProjectRoleModel
            {
                Email = email,
                ProjectId = projectId,
                Roles = data.Roles
            };
        }
        
        try
        {
            var url = $"{_controllerPath}user/{Uri.EscapeDataString(email)}/project/{projectId}/roles";
            using var client = GetHttpClient();
            
            var jsonContent = JsonConvert.SerializeObject(data);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = ValidatedResponse(client.PutAsync(url, content).Result);
            var responseContent = response.Content.ReadAsStringAsync().Result;
            
            return JsonConvert.DeserializeObject<UserProjectRoleModel>(responseContent) ?? data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting user project roles for {Email} on project {ProjectId}", email, projectId);
            return data;
        }
    }
    
    /// <summary>
    /// Remove a role from a user on a project
    /// </summary>
    public bool RemoveUserProjectRoles(string email, int projectId, int roleId)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for RemoveUserProjectRoles");
            return true;
        }
        
        try
        {
            var url = $"{_controllerPath}user/{Uri.EscapeDataString(email)}/project/{projectId}/role/{roleId}";
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.DeleteAsync(url).Result);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user project role {RoleId} for {Email} on project {ProjectId}", roleId, email, projectId);
            return false;
        }
    }
    
    /// <summary>
    /// Add a role to a user on a project
    /// </summary>
    public UserProjectRoleModel AddUserProjectRoles(string email, int projectId, int roleId)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for AddUserProjectRoles");
            return new UserProjectRoleModel
            {
                Email = email,
                ProjectId = projectId,
                Roles = new List<string> { "Role " + roleId }
            };
        }
        
        try
        {
            var url = $"{_controllerPath}user/{Uri.EscapeDataString(email)}/project/{projectId}/role/{roleId}";
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.PostAsync(url, null).Result);
            var content = response.Content.ReadAsStringAsync().Result;
            
            return JsonConvert.DeserializeObject<UserProjectRoleModel>(content) ?? 
                new UserProjectRoleModel
                {
                    Email = email,
                    ProjectId = projectId,
                    Roles = new List<string>()
                };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user project role {RoleId} for {Email} on project {ProjectId}", roleId, email, projectId);
            return new UserProjectRoleModel
            {
                Email = email,
                ProjectId = projectId,
                Roles = new List<string>()
            };
        }
    }
    
    /// <summary>
    /// Add a contact to a project
    /// </summary>
    public bool AddProjectContact(int projectId, string email)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for AddProjectContact");
            return true;
        }
        
        try
        {
            var url = $"{_controllerPath}project/{projectId}/contact/{Uri.EscapeDataString(email)}";
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.PostAsync(url, null).Result);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding project contact {Email} to project {ProjectId}", email, projectId);
            return false;
        }
    }
    
    /// <summary>
    /// Remove a contact from a project
    /// </summary>
    public bool RemovedProjectContact(int projectId, string email)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for RemovedProjectContact");
            return true;
        }
        
        try
        {
            var url = $"{_controllerPath}project/{projectId}/contact/{Uri.EscapeDataString(email)}";
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.DeleteAsync(url).Result);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing project contact {Email} from project {ProjectId}", email, projectId);
            return false;
        }
    }
    
    /// <summary>
    /// Get all available roles
    /// </summary>
    public IList<RoleModel> GetRoles()
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for GetRoles");
            return new List<RoleModel>
            {
                new RoleModel { Id = 1, Name = "Project Admin", Description = "Administrator of project" },
                new RoleModel { Id = 2, Name = "Project Member", Description = "Member of project" }
            };
        }
        
        try
        {
            var url = $"{_controllerPath}roles";
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.GetAsync(url).Result);
            var content = response.Content.ReadAsStringAsync().Result;
            var roles = JsonConvert.DeserializeObject<List<RoleModel>>(content);
            
            return roles ?? new List<RoleModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roles");
            return new List<RoleModel>();
        }
    }
    
    /// <summary>
    /// Get pending delete requests
    /// </summary>
    public IList<DeleteRequestModel> GetPendingDeletes()
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for GetPendingDeletes");
            return new List<DeleteRequestModel>
            {
                new DeleteRequestModel
                {
                    Id = 1,
                    ObjectType = "Project",
                    ObjectId = 1,
                    ObjectName = "Mock Project",
                    RequestedDate = DateTime.UtcNow.AddDays(-1),
                    RequestedBy = "test@example.com"
                }
            };
        }
        
        try
        {
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.GetAsync(_objectPendingDeletePath).Result);
            var content = response.Content.ReadAsStringAsync().Result;
            var deleteRequests = JsonConvert.DeserializeObject<List<DeleteRequestModel>>(content);
            
            return deleteRequests ?? new List<DeleteRequestModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending deletes");
            return new List<DeleteRequestModel>();
        }
    }
    
    /// <summary>
    /// Finalize a pending delete request
    /// </summary>
    public bool FinalizePendingDelete(DeleteRequestModel model)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for FinalizePendingDelete");
            return true;
        }
        
        try
        {
            var url = string.Format(_finalizeObjectDeleteRequestPath, model.Id);
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.PostAsync(url, null).Result);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing pending delete for request {RequestId}", model.Id);
            return false;
        }
    }
    
    /// <summary>
    /// Finalize project deletion
    /// </summary>
    public void FinalizeProjectDelete(int projId)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for FinalizeProjectDelete");
            return;
        }
        
        try
        {
            var url = string.Format(_projectDeletePath, projId);
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.DeleteAsync(url).Result);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Non-success status code when finalizing project delete: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing project delete for project {ProjectId}", projId);
        }
    }
    
    /// <summary>
    /// Get client ID by email
    /// </summary>
    public int? GetClientIdByEmail(string email)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for GetClientIdByEmail");
            return 1;
        }
        
        try
        {
            var userData = GetUserIntegrationData(email);
            return userData.ClientId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client ID for {Email}", email);
            return null;
        }
    }
    
    /// <summary>
    /// Get integration data for a user
    /// </summary>
    public UserIntegrationModel GetUserIntegrationData(string email)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for GetUserIntegrationData");
            return new UserIntegrationModel
            {
                Email = email,
                Name = "Mock User",
                UserId = 1,
                ClientId = 1,
                Permissions = new[] { "read", "write" },
                SystemRoles = new List<Models.SystemRoleEnum> { Models.SystemRoleEnum.Administrator },
                ProjectRoles = new List<ProjectRoleModel>
                {
                    new ProjectRoleModel
                    {
                        ProjectId = 1,
                        ProjectName = "Mock Project",
                        Roles = new List<string> { "Project Admin" }
                    }
                }
            };
        }
        
        UserIntegrationModel retval = null;
        using (var client = GetHttpClient())
        {
            try
            {
                var encodedEmail = System.Web.HttpUtility.UrlEncode(email); // Encode only the email
                var resourcePath = String.Format(_userIntegrationPath, encodedEmail); // Insert encoded email into the path
                var response = ValidatedResponse(client.GetAsync(resourcePath).Result);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    retval = JsonConvert.DeserializeObject<UserIntegrationModel>(response.Content.ReadAsStringAsync().Result);
                    
                    // Post-process permissions to remove spaces and slashes
                    if (retval?.Permissions != null)
                    {
                        for (int i = 0; i < retval.Permissions.Length; i++)
                        {
                            if (retval.Permissions[i].Contains(" ") || retval.Permissions[i].Contains("/"))
                                retval.Permissions[i] = retval.Permissions[i].Replace(" ", string.Empty).Replace("/", string.Empty);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error getting user integration data for {Email}", email);
                System.Diagnostics.Debug.WriteLine(ex.Message);
                throw;
            }
        }
        return retval ?? new UserIntegrationModel 
        { 
            Email = email,
            Name = "Unknown User",
            UserId = 0,
            ClientId = null,
            Permissions = Array.Empty<string>(),
            SystemRoles = new List<Models.SystemRoleEnum>(),
            ProjectRoles = new List<ProjectRoleModel>()
        };
    }
    
    /// <summary>
    /// Get allowed actions for Estacker
    /// </summary>
    public string[] GetEstackerAllowedActions(string email, int projectId)
    {
        var useMockData = _configuration.GetValue<bool>("ArrayAPI:UseMockData", true);
        
        if (useMockData || string.IsNullOrEmpty(_arrayApiBaseAddress))
        {
            _logger.LogInformation("Using mock data for GetEstackerAllowedActions");
            return new[] { "read", "write", "delete" };
        }
        
        try
        {
            var url = $"{_controllerPath}user/{Uri.EscapeDataString(email)}/project/{projectId}/allowed-actions";
            using var client = GetHttpClient();
            
            var response = ValidatedResponse(client.GetAsync(url).Result);
            var content = response.Content.ReadAsStringAsync().Result;
            var allowedActions = JsonConvert.DeserializeObject<string[]>(content);
            
            return allowedActions ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Estacker allowed actions for {Email} on project {ProjectId}", email, projectId);
            return Array.Empty<string>();
        }
    }
    
    /// <summary>
    /// Fallback method to determine roles from database AccessLevel
    /// </summary>
    private async Task<List<SystemRole>?> GetUserSystemRolesFromDatabaseAsync(string userEmail)
    {
        try
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            const string sql = "SELECT AccessLevel FROM Users WHERE UserEmail = @UserEmail AND Active = 1 AND Deleted = 0";
            var accessLevel = await connection.QueryFirstOrDefaultAsync<int?>(sql, new { UserEmail = userEmail });

            if (!accessLevel.HasValue)
            {
                _logger.LogWarning("User not found in database: {Email}", userEmail);
                return null;
            }

            var roles = new List<SystemRole>();

            // Map database AccessLevel to SystemRoles (based on legacy logic)
            switch (accessLevel.Value)
            {
                case 0:
                case 1:
                    roles.Add(SystemRole.Administrator);
                    roles.Add(SystemRole.Manager);
                    roles.Add(SystemRole.Analyst);
                    roles.Add(SystemRole.Viewer);
                    break;
                case 3:
                    roles.Add(SystemRole.Manager);
                    roles.Add(SystemRole.Analyst);
                    roles.Add(SystemRole.Viewer);
                    break;
                case 5:
                    roles.Add(SystemRole.Analyst);
                    roles.Add(SystemRole.Viewer);
                    break;
                case 7:
                default:
                    roles.Add(SystemRole.Viewer);
                    break;
            }

            _logger.LogInformation("Mapped AccessLevel {AccessLevel} to roles [{Roles}] for user {Email}", 
                accessLevel.Value, string.Join(", ", roles), userEmail);

            return roles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user roles from database for {Email}", userEmail);
            return null;
        }
    }
}
