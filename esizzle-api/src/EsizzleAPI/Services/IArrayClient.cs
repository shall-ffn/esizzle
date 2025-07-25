using EsizzleAPI.Models;
using System.Net;

namespace EsizzleAPI.Services;

public interface IArrayClient
{
    /// <summary>
    /// Get user system roles from ArrayAPI (async version)
    /// </summary>
    Task<List<SystemRole>?> GetUserSystemRolesAsync(string userEmail);
    
    /// <summary>
    /// Get user system roles from ArrayAPI (sync version for legacy compatibility)
    /// </summary>
    IList<SystemRoleEnum> GetUserSystemRoles(string email);
    
    /// <summary>
    /// Check if user has Administrator role
    /// </summary>
    Task<bool> IsUserAdministratorAsync(string userEmail);
    
    /// <summary>
    /// Update project metrics
    /// </summary>
    bool UpdateProjectMetrics(int projectId, ProjectUpdateModel update);
    
    /// <summary>
    /// Get projects pending deletion
    /// </summary>
    IList<ProjectModel> GetProjectsPendingDelete();
    
    /// <summary>
    /// Get all projects
    /// </summary>
    IList<ProjectModel> GetProjects();
    
    /// <summary>
    /// Get roles for a user on a specific project
    /// </summary>
    IList<string> GetUserProjectRole(string email, int projectId);
    
    /// <summary>
    /// Set roles for a user on a specific project
    /// </summary>
    UserProjectRoleModel SetUserProjectRoles(string email, int projectId, UserProjectRoleModel data);
    
    /// <summary>
    /// Remove a role from a user on a project
    /// </summary>
    bool RemoveUserProjectRoles(string email, int projectId, int roleId);
    
    /// <summary>
    /// Add a role to a user on a project
    /// </summary>
    UserProjectRoleModel AddUserProjectRoles(string email, int projectId, int roleId);
    
    /// <summary>
    /// Add a contact to a project
    /// </summary>
    bool AddProjectContact(int projectId, string email);
    
    /// <summary>
    /// Remove a contact from a project
    /// </summary>
    bool RemovedProjectContact(int projectId, string email);
    
    /// <summary>
    /// Get all available roles
    /// </summary>
    IList<RoleModel> GetRoles();
    
    /// <summary>
    /// Get pending delete requests
    /// </summary>
    IList<DeleteRequestModel> GetPendingDeletes();
    
    /// <summary>
    /// Finalize a pending delete request
    /// </summary>
    bool FinalizePendingDelete(DeleteRequestModel model);
    
    /// <summary>
    /// Finalize project deletion
    /// </summary>
    void FinalizeProjectDelete(int projId);
    
    /// <summary>
    /// Get client ID by email
    /// </summary>
    int? GetClientIdByEmail(string email);
    
    /// <summary>
    /// Get integration data for a user
    /// </summary>
    UserIntegrationModel GetUserIntegrationData(string email);
    
    /// <summary>
    /// Get allowed actions for Estacker
    /// </summary>
    string[] GetEstackerAllowedActions(string email, int projectId);
}

public enum SystemRole
{
    Administrator,
    Analyst,
    Viewer,
    Manager
}

// Legacy enum for compatibility
public enum SystemRoleEnum
{
    Administrator,
    Analyst, 
    Viewer,
    Manager
}