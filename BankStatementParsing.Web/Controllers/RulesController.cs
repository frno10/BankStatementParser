using BankStatementParsing.Core.Models;
using BankStatementParsing.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankStatementParsing.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RulesController : ControllerBase
{
    private readonly ITransactionRuleService _ruleService;
    private readonly ILogger<RulesController> _logger;

    public RulesController(
        ITransactionRuleService ruleService,
        ILogger<RulesController> logger)
    {
        _ruleService = ruleService;
        _logger = logger;
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<List<TransactionRule>>> GetRules(int userId)
    {
        try
        {
            var rules = await _ruleService.GetRulesAsync(userId);
            return Ok(rules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rules for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<TransactionRule>> CreateRule([FromBody] TransactionRule rule)
    {
        try
        {
            var createdRule = await _ruleService.CreateRuleAsync(rule);
            return CreatedAtAction(nameof(GetRules), new { userId = rule.UserId }, createdRule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rule");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{ruleId}")]
    public async Task<ActionResult<TransactionRule>> UpdateRule(int ruleId, [FromBody] TransactionRule rule)
    {
        try
        {
            if (ruleId != rule.Id)
                return BadRequest(new { error = "Rule ID mismatch" });

            var updatedRule = await _ruleService.UpdateRuleAsync(rule);
            return Ok(updatedRule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rule {RuleId}", ruleId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{ruleId}")]
    public async Task<ActionResult> DeleteRule(int ruleId)
    {
        try
        {
            await _ruleService.DeleteRuleAsync(ruleId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rule {RuleId}", ruleId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{userId}/apply-all")]
    public async Task<ActionResult> ApplyAllRules(int userId)
    {
        try
        {
            var appliedCount = await _ruleService.ApplyRulesToAllTransactionsAsync(userId);
            return Ok(new { appliedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying rules for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }
}