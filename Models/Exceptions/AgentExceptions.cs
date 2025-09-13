using System;

namespace AmalaSpotLocator.Models.Exceptions;

public class AgentException : Exception
{
    public string ErrorCode { get; set; }
    
    public AgentException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
    
    public AgentException(string message, string errorCode, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

public class NLUAgentException : AgentException
{
    public NLUAgentException(string message) 
        : base(message, "NLU_AGENT_ERROR") { }
        
    public NLUAgentException(string message, Exception innerException) 
        : base(message, "NLU_AGENT_ERROR", innerException) { }
}

public class QueryAgentException : AgentException
{
    public QueryAgentException(string message) 
        : base(message, "QUERY_AGENT_ERROR") { }
        
    public QueryAgentException(string message, Exception innerException) 
        : base(message, "QUERY_AGENT_ERROR", innerException) { }
}

public class ResponseAgentException : AgentException
{
    public ResponseAgentException(string message) 
        : base(message, "RESPONSE_AGENT_ERROR") { }
        
    public ResponseAgentException(string message, Exception innerException) 
        : base(message, "RESPONSE_AGENT_ERROR", innerException) { }
}

public class OrchestrationException : AgentException
{
    public OrchestrationException(string message) 
        : base(message, "ORCHESTRATION_ERROR") { }
        
    public OrchestrationException(string message, Exception innerException) 
        : base(message, "ORCHESTRATION_ERROR", innerException) { }
}