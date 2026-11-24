namespace OfficeTraining.Scenario
{
    /// <summary>
    /// All learner-facing assessment text in one place so it can be reviewed against official
    /// local fire-safety guidance before being taught. Keep the wording neutral: it describes
    /// what happened in the simulation, not professional emergency procedures.
    /// </summary>
    public static class FeedbackText
    {
        public const string Disclaimer =
            "Student prototype. This feedback describes the simulation only and is not certified fire-safety training.";

        // Hazard inspection
        public const string HazardsTitle = "Identify hazards";
        public const string HazardsCriterion = "Confirm at least {0} of the {1} hazards in the office.";
        public const string HazardsAllFound = "You found all {0} hazards.";
        public const string HazardsEnough = "You found {0} of {1} hazards. Not found: {2}.";
        public const string HazardsTooFew = "You found {0} of {1} hazards; at least {2} were needed. Look again at: {3}.";
        public const string HazardsNone = "No hazards were part of this scenario.";

        // Decision
        public const string DecisionTitle = "Safe response decision";
        public const string DecisionCriterion =
            "Choose to evacuate, or practice with the extinguisher only when the scenario permits it, and do not use it outside that practice.";
        public const string DecisionEvacuated = "You chose to evacuate.";
        public const string DecisionEvacuatedImplicit = "You went straight to the assembly point, which counts as evacuating.";
        public const string DecisionPracticePermitted = "You practiced only in the permitted, supervised condition.";
        public const string DecisionNotPermitted =
            "Practice was not permitted in this scenario. In that situation the expected choice was to evacuate.";
        public const string DecisionUnsafeUse = "The extinguisher was used outside the permitted practice ({0} time(s)).";
        public const string DecisionNone = "No response decision was recorded before the session ended.";

        // Practice
        public const string PracticeTitle = "Extinguisher practice";
        public const string PracticeCriterion = "If you choose the permitted practice, complete the practice interaction.";
        public const string PracticeNotApplicable = "Practice was not part of your route.";
        public const string PracticeSucceeded = "You completed the practice interaction.";
        public const string PracticeFailed = "The practice interaction was not completed successfully.";
        public const string PracticeAbandoned = "You stopped the practice and evacuated. Evacuating is always an acceptable choice.";
        public const string PracticeNotFinished = "The session ended before the practice was finished.";

        // Evacuation
        public const string EvacuationTitle = "Reach the assembly point";
        public const string EvacuationCriterion = "Follow the marked exit route and reach the assembly point.";
        public const string EvacuationReached = "You reached the assembly point.";
        public const string EvacuationMissed = "The assembly point was not reached.";
    }
}
