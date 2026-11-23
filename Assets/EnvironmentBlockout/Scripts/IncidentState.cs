namespace OfficeTraining.Environment
{
    /// <summary>Environment presentation state, not a training phase or a safety assessment.</summary>
    public enum IncidentState
    {
        Inactive = 0,
        Active = 1,
        Resolved = 2,
        EvacuationRequired = 3
    }
}
