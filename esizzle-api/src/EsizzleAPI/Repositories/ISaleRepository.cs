using EsizzleAPI.Models;

namespace EsizzleAPI.Repositories;

public interface ISaleRepository
{
    Task<IEnumerable<SaleSummaryModel>> GetByOfferingIdAsync(int offeringId);
    Task<SaleModel?> GetByIdAsync(int saleId);
    Task<bool> BelongsToOfferingAsync(int saleId, int offeringId);
}