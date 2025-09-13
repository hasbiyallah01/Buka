using System.Collections.Generic;
using AmalaSpotLocator.Models.UserModel;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Interfaces;

public interface IQueryAgent
{
    Task<QueryResult> ExecuteSpotSearch(UserIntent intent);
    Task<QueryResult> ExecuteSpotDetails(UserIntent intent);
    Task<QueryResult> ExecuteAddSpot(UserIntent intent);
    Task<QueryResult> ExecuteReviewQuery(UserIntent intent);
    Task<QueryResult> ExecuteGenericQuery(UserIntent intent);
}