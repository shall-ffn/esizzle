using EsizzleAPI.Models;

namespace EsizzleAPI.Repositories;

public interface IOfferingRepository
{
    Task<IEnumerable<OfferingSummaryModel>> GetUserOfferingsAsync(int userId);
    Task<OfferingModel?> GetByIdAsync(int offeringId);
    Task<bool> HasUserAccessAsync(int userId, int offeringId);
    Task<IEnumerable<OfferingSummaryModel>> GetVisibleOfferingsAsync();
}