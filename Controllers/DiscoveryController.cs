using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Core.Domain.Entities;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/discover")]
public class DiscoveryController : ControllerBase
{
    private readonly ISpotDiscoveryService _discoveryService;
    private readonly ILogger<DiscoveryController> _logger;

    public DiscoveryController(
        ISpotDiscoveryService discoveryService,
        ILogger<DiscoveryController> logger)
    {
        _discoveryService = discoveryService;
        _logger = logger;
    }

    [HttpPost("run")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<DiscoveryResult>> RunDiscovery(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Manual discovery run triggered by user");
            var result = await _discoveryService.RunDiscoveryAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running manual discovery: {Error}", ex.Message);
            return StatusCode(500, new { error = "Failed to run discovery pipeline", details = ex.Message });
        }
    }

    [HttpGet("metrics")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<DiscoveryMetrics>> GetMetrics(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = await _discoveryService.GetDiscoveryMetricsAsync(fromDate, toDate, cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discovery metrics: {Error}", ex.Message);
            return StatusCode(500, new { error = "Failed to get discovery metrics", details = ex.Message });
        }
    }

    [HttpGet("candidates")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<IEnumerable<SpotCandidate>>> GetCandidates(
        [FromQuery] CandidateStatus? status = null,
        [FromQuery] DiscoverySource? source = null,
        [FromQuery] double? minConfidenceScore = null,
        [FromQuery] double? minQualityScore = null,
        [FromQuery] DateTime? discoveredAfter = null,
        [FromQuery] DateTime? discoveredBefore = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = new CandidateFilter
            {
                Status = status,
                Source = source,
                MinConfidenceScore = minConfidenceScore,
                MinQualityScore = minQualityScore,
                DiscoveredAfter = discoveredAfter,
                DiscoveredBefore = discoveredBefore,
                Limit = Math.Min(limit, 100), // Cap at 100
                Offset = Math.Max(offset, 0)
            };

            var candidates = await _discoveryService.GetCandidatesAsync(filter, cancellationToken);
            return Ok(candidates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting candidates: {Error}", ex.Message);
            return StatusCode(500, new { error = "Failed to get candidates", details = ex.Message });
        }
    }

    [HttpPost("candidates/{candidateId}/approve")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<AmalaSpot>> ApproveCandidate(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        try
        {

            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("id");
            var userId = userIdClaim != null ? Guid.Parse(userIdClaim.Value) : (Guid?)null;

            var spot = await _discoveryService.ApproveCandidateAsync(candidateId, userId, cancellationToken);
            
            _logger.LogInformation("Candidate {CandidateId} approved by user {UserId}, created spot {SpotId}", 
                candidateId, userId, spot.Id);
            
            return Ok(spot);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving candidate {CandidateId}: {Error}", candidateId, ex.Message);
            return StatusCode(500, new { error = "Failed to approve candidate", details = ex.Message });
        }
    }

    [HttpPost("candidates/{candidateId}/reject")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult> RejectCandidate(
        Guid candidateId,
        [FromBody] RejectCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { error = "Rejection reason is required" });

            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("id");
            var userId = userIdClaim != null ? Guid.Parse(userIdClaim.Value) : (Guid?)null;

            await _discoveryService.RejectCandidateAsync(candidateId, request.Reason, userId, cancellationToken);
            
            _logger.LogInformation("Candidate {CandidateId} rejected by user {UserId} with reason: {Reason}", 
                candidateId, userId, request.Reason);
            
            return Ok(new { message = "Candidate rejected successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting candidate {CandidateId}: {Error}", candidateId, ex.Message);
            return StatusCode(500, new { error = "Failed to reject candidate", details = ex.Message });
        }
    }

    [HttpPost("web-scraping")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<List<SpotCandidate>>> DiscoverFromWebScraping(CancellationToken cancellationToken = default)
    {
        try
        {
            var candidates = await _discoveryService.DiscoverFromWebScrapingAsync(cancellationToken);
            return Ok(candidates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in web scraping discovery: {Error}", ex.Message);
            return StatusCode(500, new { error = "Failed to discover from web scraping", details = ex.Message });
        }
    }

    [HttpPost("google-places")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<List<SpotCandidate>>> DiscoverFromGooglePlaces(CancellationToken cancellationToken = default)
    {
        try
        {
            var candidates = await _discoveryService.DiscoverFromGooglePlacesAsync(cancellationToken);
            return Ok(candidates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Google Places discovery: {Error}", ex.Message);
            return StatusCode(500, new { error = "Failed to discover from Google Places", details = ex.Message });
        }
    }

    [HttpPost("social-media")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<List<SpotCandidate>>> DiscoverFromSocialMedia(CancellationToken cancellationToken = default)
    {
        try
        {
            var candidates = await _discoveryService.DiscoverFromSocialMediaAsync(cancellationToken);
            return Ok(candidates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in social media discovery: {Error}", ex.Message);
            return StatusCode(500, new { error = "Failed to discover from social media", details = ex.Message });
        }
    }
}

public class RejectCandidateRequest
{
    public string Reason { get; set; } = string.Empty;
}