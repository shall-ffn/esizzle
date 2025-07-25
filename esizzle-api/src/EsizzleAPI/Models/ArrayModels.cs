using System.Text.Json.Serialization;

namespace EsizzleAPI.Models;

/// <summary>
/// Token model for Array API authentication
/// </summary>
public class Token
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

/// <summary>
/// Model for updating project metrics
/// </summary>
public class ProjectUpdateModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ClientId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// Project model for Array API
/// </summary>
public class ProjectModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public int? ClientId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// Model for user project roles
/// </summary>
public class UserProjectRoleModel
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public List<string> Roles { get; set; } = new List<string>();
}

/// <summary>
/// Role model for Array API
/// </summary>
public class RoleModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Model for delete requests
/// </summary>
public class DeleteRequestModel
{
    public int Id { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public int ObjectId { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
}

/// <summary>
/// Model for user integration data
/// </summary>
public class UserIntegrationModel
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ClientId { get; set; }
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public List<SystemRoleEnum> SystemRoles { get; set; } = new List<SystemRoleEnum>();
    public List<ProjectRoleModel> ProjectRoles { get; set; } = new List<ProjectRoleModel>();
}

/// <summary>
/// Model for project roles
/// </summary>
public class ProjectRoleModel
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new List<string>();
}

/// <summary>
/// Exception for connectivity issues with Array API
/// </summary>
public class ConnectivityException : Exception
{
    public ConnectivityException(string message) : base(message) { }
}

/// <summary>
/// Exception for server errors from Array API
/// </summary>
public class ServerErrorException : Exception
{
    public ServerErrorException(string message) : base(message) { }
}
