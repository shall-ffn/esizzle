using EsizzleAPI.Models;

namespace EsizzleAPI.Repositories;

public interface IDocumentRepository
{
    Task<IEnumerable<DocumentSummaryModel>> GetByLoanIdAsync(int loanId);
    Task<DocumentModel?> GetByIdAsync(int documentId);
    Task<bool> BelongsToLoanAsync(int documentId, int loanId);
    Task<string> GenerateDocumentUrlAsync(int documentId);
    Task<bool> UpdateDocumentTypeAsync(int documentId, string documentType, int userId);
    Task<bool> MarkAsRedactedAsync(int documentId, int userId);
    Task<bool> UpdateProcessingStatusAsync(int documentId, int statusId);
    Task<IEnumerable<string>> GetDocumentTypesAsync();
    Task<IEnumerable<DocumentTypeModel>> GetDocumentTypesByOfferingAsync(int offeringId);
    Task<bool> UpdateDocumentClassificationAsync(int documentId, int docTypeId, int userId);
    Task<IEnumerable<ImageOfferingActionModel>> GetOfferingImageActionsAsync(int offeringId);
}
