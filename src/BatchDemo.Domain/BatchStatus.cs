namespace BatchDemo.Domain;

public enum BatchStatus
{
    Received,
    Duplicate,
    Ready,
    ReadyWithExceptions,
    Rejected,
    ProcessingFailed
}
