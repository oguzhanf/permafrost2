using Microsoft.AspNetCore.Mvc;
using Permafrost2.Api.Services;
using Permafrost2.Shared.DTOs;

namespace Permafrost2.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AgentRegistrationResponse>> RegisterAgent([FromBody] AgentRegistrationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _agentService.RegisterAgentAsync(request);
        
        if (response.Success)
        {
            return Ok(response);
        }
        
        return BadRequest(response);
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<AgentHeartbeatResponse>> Heartbeat([FromBody] AgentHeartbeatRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _agentService.ProcessHeartbeatAsync(request);
        
        if (response.Success)
        {
            return Ok(response);
        }
        
        return BadRequest(response);
    }

    [HttpPost("submit-data")]
    public async Task<ActionResult<DataSubmissionResponse>> SubmitData([FromBody] DataSubmissionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _agentService.ProcessDataSubmissionAsync(request);
        
        if (response.Success)
        {
            return Ok(response);
        }
        
        return BadRequest(response);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AgentStatusDto>>> GetAgents()
    {
        var agents = await _agentService.GetAgentsAsync();
        return Ok(agents);
    }

    [HttpGet("{agentId}")]
    public async Task<ActionResult<AgentStatusDto>> GetAgent(Guid agentId)
    {
        var agent = await _agentService.GetAgentAsync(agentId);
        
        if (agent == null)
        {
            return NotFound();
        }
        
        return Ok(agent);
    }

    [HttpPut("{agentId}/configuration")]
    public async Task<ActionResult> UpdateConfiguration(Guid agentId, [FromBody] AgentConfigurationDto configuration)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var success = await _agentService.UpdateAgentConfigurationAsync(agentId, configuration);
        
        if (success)
        {
            return Ok();
        }
        
        return NotFound();
    }

    [HttpDelete("{agentId}")]
    public async Task<ActionResult> DeactivateAgent(Guid agentId)
    {
        var success = await _agentService.DeactivateAgentAsync(agentId);
        
        if (success)
        {
            return Ok();
        }
        
        return NotFound();
    }

    [HttpGet("{agentId}/submissions")]
    public async Task<ActionResult> GetAgentSubmissions(Guid agentId, [FromQuery] int limit = 50)
    {
        var submissions = await _agentService.GetAgentSubmissionsAsync(agentId, limit);
        return Ok(submissions);
    }

    [HttpGet("version")]
    public ActionResult<object> GetVersion()
    {
        return Ok(new { 
            Version = "1.0.0", 
            ApiVersion = "v1",
            Timestamp = DateTime.UtcNow 
        });
    }
}
