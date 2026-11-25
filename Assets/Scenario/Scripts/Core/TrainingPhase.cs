namespace OfficeTraining.Scenario
{
    /// <summary>
    /// Training phases in scenario order. Pause and reset are cross-cutting controls, not phases.
    /// Introduction -> Inspection -> Incident -> Decision -> Practice or Evacuation -> Completed.
    /// </summary>
    public enum TrainingPhase
    {
        Introduction = 0,
        Inspection = 1,
        Incident = 2,
        Decision = 3,
        Practice = 4,
        Evacuation = 5,
        Completed = 6
    }

    /// <summary>The response the learner chose once the incident started.</summary>
    public enum DecisionPath
    {
        None = 0,
        Evacuate = 1,
        PracticeInPermittedCondition = 2,
        PracticeWhenNotPermitted = 3
    }

    /// <summary>Outcome of the supervised extinguisher practice branch.</summary>
    public enum PracticeResult
    {
        NotAttempted = 0,
        Succeeded = 1,
        Failed = 2,
        Abandoned = 3,
        NotPermitted = 4
    }
}
