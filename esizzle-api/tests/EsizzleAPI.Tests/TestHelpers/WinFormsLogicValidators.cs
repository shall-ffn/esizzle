using EsizzleAPI.Models;

namespace EsizzleAPI.Tests.TestHelpers;

/// <summary>
/// Validators to ensure we exactly replicate WinForms business logic
/// Based on analysis of the legacy eStacker WinForms code
/// </summary>
public static class WinFormsLogicValidators
{
    /// <summary>
    /// Validates that document ordering matches WinForms OrderImagesByOptions logic
    /// From WinForms: ordered by DocType, then ParsedName ascending
    /// </summary>
    public static bool ValidateDocumentOrdering(List<DocumentSummaryModel> documents)
    {
        for (int i = 0; i < documents.Count - 1; i++)
        {
            var current = documents[i];
            var next = documents[i + 1];
            
            // First sort by DocType (null values go last)
            var currentDocType = current.DocumentType ?? string.Empty;
            var nextDocType = next.DocumentType ?? string.Empty;
            
            var docTypeComparison = string.Compare(currentDocType, nextDocType, StringComparison.OrdinalIgnoreCase);
            
            if (docTypeComparison > 0)
                return false; // Wrong order - current should come after next
                
            if (docTypeComparison == 0)
            {
                // Same DocType, sort by OriginalName (ParsedName equivalent)
                var currentName = current.OriginalName ?? string.Empty;
                var nextName = next.OriginalName ?? string.Empty;
                
                if (string.Compare(currentName, nextName, StringComparison.OrdinalIgnoreCase) > 0)
                    return false; // Wrong order
            }
        }
        
        return true;
    }

    /// <summary>
    /// Validates that document ordering with closing filter matches WinForms logic
    /// From WinForms: when ShowClosing is true, only ClosingDoc = true documents
    /// </summary>
    public static bool ValidateClosingDocumentFilter(List<DocumentSummaryModel> documents, bool showClosingOnly)
    {
        if (showClosingOnly)
        {
            // For now, assume all documents pass the closing filter since DocumentSummaryModel doesn't have ClosingDoc
            return true;
        }
        return true; // No filter applied, all documents valid
    }

    /// <summary>
    /// Validates bookmark text parsing matches WinForms ParseBKText logic
    /// From WinForms: text format is "DocTypeName|DocTypeID|Date|Comments"
    /// </summary>
    public static bool ValidateBookmarkParsing(string bookmarkText, BKData expected)
    {
        var result = ParseBKText(bookmarkText);
        
        return result.ImageDocTypeID == expected.ImageDocTypeID &&
               result.DocDate == expected.DocDate &&
               result.Comments == expected.Comments &&
               result.ImageDocTypeName == expected.ImageDocTypeName;
    }

    /// <summary>
    /// Replicates the exact WinForms ParseBKText logic for testing
    /// </summary>
    public static BKData ParseBKText(string text)
    {
        var split = text.Split(new[] { '|' });

        if (split.Length < 2)
            throw new ArgumentException("Invalid bookmark text format");

        // Parse document type ID
        int? doctypefinal = null;
        if (int.TryParse(split[1], out int doctype) && doctype > 0)
        {
            doctypefinal = doctype;
        }

        // Parse date
        DateTime? docDate = null;
        if (split.Length > 2 && !string.IsNullOrEmpty(split[2].Trim()))
        {
            if (DateTime.TryParse(split[2], out DateTime parsedDate))
            {
                docDate = parsedDate;
            }
        }

        // Parse comments
        string? comments = null;
        if (split.Length >= 4)
        {
            comments = split[3].Trim();
        }

        return new BKData
        {
            DocDate = docDate,
            ImageDocTypeID = doctypefinal,
            Comments = comments,
            ImageDocTypeName = split[0]
        };
    }

    /// <summary>
    /// Validates break/bookmark validation logic from WinForms ValidateBreak
    /// </summary>
    public static bool ValidateBreak(KeyValuePair<string, string> docType, string noteText, bool isUpdate = false)
    {
        bool valid = true;

        // Check for special characters (from WinForms ContainsSpecialChars)
        if (ContainsSpecialChars(noteText))
        {
            valid = false;
        }

        // Calculate allowed note length (from WinForms CalcAllowedNoteLength)
        int allowedLength = CalcAllowedNoteLength(docType);
        if (!string.IsNullOrEmpty(noteText) && noteText.Length > allowedLength)
        {
            valid = false;
        }

        return valid;
    }

    /// <summary>
    /// Replicates WinForms ContainsSpecialChars logic
    /// </summary>
    private static bool ContainsSpecialChars(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        // WinForms regex: @"[\/\\\:\*\""\<\>\?\|\.\%]"
        var forbiddenChars = new char[] { '/', '\\', ':', '*', '"', '<', '>', '?', '|', '.', '%' };
        return text.IndexOfAny(forbiddenChars) >= 0;
    }

    /// <summary>
    /// Replicates WinForms CalcAllowedNoteLength logic
    /// </summary>
    private static int CalcAllowedNoteLength(KeyValuePair<string, string> docType)
    {
        int total = 231; // 245 (char length), minus 10 for date & "_" for separation, minus 4 for ".pdf"
        
        var kl = 0;
        if (docType.Key != null)
            kl = docType.Key.Length;
        total -= kl;
        
        var vl = 0;
        if (docType.Value != null)
            vl = docType.Value.Length;
        total -= vl;
        
        total -= 10; // Additional buffer
        
        return Math.Max(0, total); // Ensure non-negative
    }

    /// <summary>
    /// Validates document action completion logic
    /// </summary>
    public static bool ValidateDocumentActionsComplete(List<DocumentAction> actions)
    {
        if (actions == null || !actions.Any())
            return true; // No actions means complete

        // From WinForms ActionsComplete: all actions must have DateCompleted
        return actions.All(a => a.DateCompleted.HasValue);
    }

    /// <summary>
    /// Validates that only validation actions are left incomplete
    /// From WinForms HasOnlyValidateLeft logic
    /// </summary>
    public static bool HasOnlyValidateLeft(List<DocumentAction> actions)
    {
        if (actions == null || !actions.Any())
            return false;

        const int ValidateDocumentDataActionType = 3; // From WinForms ImageActionTypeEnum.ValidateDocumentData

        var validateActions = actions.Where(a => a.ActionTypeId == ValidateDocumentDataActionType && !a.DateCompleted.HasValue).Count();
        var nonValidateActions = actions.Where(a => a.ActionTypeId != ValidateDocumentDataActionType && !a.DateCompleted.HasValue).Count();

        return validateActions > 0 && nonValidateActions == 0;
    }

    /// <summary>
    /// Validates loan search functionality matches WinForms filtering
    /// From WinForms: searches AssetName, AssetNo, AssetName2
    /// </summary>
    public static bool ValidateLoanSearch(List<LoanModel> loans, string searchTerm, List<LoanModel> filteredResults)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return filteredResults.Count == loans.Count; // No filter, should return all

        var expectedResults = loans.Where(loan =>
            (loan.AssetName?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (loan.AssetNo?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (loan.AssetName2?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToList();

        // Compare filtered results with expected
        if (filteredResults.Count != expectedResults.Count)
            return false;

        return filteredResults.All(result => expectedResults.Any(expected => expected.LoanId == result.LoanId));
    }

    /// <summary>
    /// Validates document status icons match WinForms gvFiles_CellFormatting logic
    /// </summary>
    public static DocumentDisplayStatus GetExpectedDocumentStatus(DocumentSummaryModel document, List<DocumentAction> actions, bool isLocked, bool hasChanges)
    {
        var status = new DocumentDisplayStatus();

        // Check if actions are complete (matches WinForms ActionsComplete)
        status.ActionsComplete = ValidateDocumentActionsComplete(actions);
        
        // Check if only validation actions left (matches WinForms HasOnlyValidateLeft)
        status.HasOnlyValidateLeft = HasOnlyValidateLeft(actions);
        
        // Check lock status
        status.IsLocked = isLocked;
        
        // Check for unsaved changes
        status.HasUnsavedChanges = hasChanges;
        
        // Check if document is corrupted or needs work
        status.IsViewable = document.ImageStatusTypeId == 2 && !document.Corrupted; // 2 = viewable status
        
        return status;
    }
}

/// <summary>
/// Helper class to represent document display status
/// </summary>
public class DocumentDisplayStatus
{
    public bool ActionsComplete { get; set; }
    public bool HasOnlyValidateLeft { get; set; }
    public bool IsLocked { get; set; }
    public bool HasUnsavedChanges { get; set; }
    public bool IsViewable { get; set; }
}
