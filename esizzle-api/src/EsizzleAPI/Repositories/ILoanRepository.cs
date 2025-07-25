using EsizzleAPI.Models;

namespace EsizzleAPI.Repositories;

public interface ILoanRepository
{
    Task<IEnumerable<LoanSummaryModel>> GetBySaleIdAsync(int saleId);
    Task<LoanModel?> GetByIdAsync(int loanId);
    Task<bool> BelongsToSaleAsync(int loanId, int saleId);
    Task<IEnumerable<LoanSummaryModel>> SearchLoansAsync(int saleId, string searchTerm);
}