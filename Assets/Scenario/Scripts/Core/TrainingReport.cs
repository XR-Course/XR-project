using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OfficeTraining.Scenario
{
    public enum ObjectiveStatus
    {
        Achieved,
        Missed,
        NotApplicable
    }

    /// <summary>One learning objective with its pass criterion stated in plain words.</summary>
    public sealed class ObjectiveResult
    {
        public ObjectiveResult(string id, string title, string criterion, ObjectiveStatus status, string feedback)
        {
            Id = id;
            Title = title;
            Criterion = criterion;
            Status = status;
            Feedback = feedback;
        }

        /// <summary>Stable id for UI lookups: see AssessmentService.ObjectiveIds.</summary>
        public string Id { get; }
        public string Title { get; }
        public string Criterion { get; }
        public ObjectiveStatus Status { get; }
        public string Feedback { get; }
    }

    /// <summary>Read-only result data for the results screen. Contains no score, only objectives.</summary>
    public sealed class TrainingReport
    {
        internal TrainingReport(bool completed, bool endedEarly, double durationSeconds,
            IReadOnlyList<HazardInfo> hazardsFound, IReadOnlyList<HazardInfo> hazardsMissed, int requiredHazardCount,
            DecisionPath decision, bool decisionWasImplicit, PracticeResult practice, bool reachedSafeZone,
            int unsafeActionCount, IReadOnlyList<ObjectiveResult> objectives, IReadOnlyList<SessionEvent> events)
        {
            Completed = completed;
            EndedEarly = endedEarly;
            DurationSeconds = durationSeconds;
            HazardsFound = hazardsFound;
            HazardsMissed = hazardsMissed;
            RequiredHazardCount = requiredHazardCount;
            Decision = decision;
            DecisionWasImplicit = decisionWasImplicit;
            Practice = practice;
            ReachedSafeZone = reachedSafeZone;
            UnsafeActionCount = unsafeActionCount;
            Objectives = objectives;
            Events = events;

            foreach (ObjectiveResult objective in objectives)
            {
                if (objective.Status == ObjectiveStatus.NotApplicable)
                    continue;
                ObjectivesApplicable++;
                if (objective.Status == ObjectiveStatus.Achieved)
                    ObjectivesAchieved++;
            }
        }

        public bool Completed { get; }
        public bool EndedEarly { get; }
        public double DurationSeconds { get; }
        public IReadOnlyList<HazardInfo> HazardsFound { get; }
        public IReadOnlyList<HazardInfo> HazardsMissed { get; }
        public int RequiredHazardCount { get; }
        public DecisionPath Decision { get; }
        public bool DecisionWasImplicit { get; }
        public PracticeResult Practice { get; }
        public bool ReachedSafeZone { get; }
        public int UnsafeActionCount { get; }
        public IReadOnlyList<ObjectiveResult> Objectives { get; }
        public int ObjectivesAchieved { get; }
        public int ObjectivesApplicable { get; }
        /// <summary>Snapshot of the session log at the time of the report.</summary>
        public IReadOnlyList<SessionEvent> Events { get; }
        public string Disclaimer => FeedbackText.Disclaimer;

        public ObjectiveResult GetObjective(string id)
        {
            foreach (ObjectiveResult objective in Objectives)
                if (objective.Id == id)
                    return objective;
            return null;
        }

        /// <summary>Plain-text summary for the console, logs and the mock simulation.</summary>
        public string ToText(bool includeEventLog = false)
        {
            var text = new StringBuilder();
            text.AppendLine("=== Training report ===");
            text.Append("Status: ").AppendLine(EndedEarly ? "ended early" : Completed ? "completed" : "in progress");
            text.Append("Duration: ").Append(DurationSeconds.ToString("0.0", CultureInfo.InvariantCulture)).AppendLine(" s");
            text.Append("Objectives achieved: ").Append(ObjectivesAchieved).Append('/').Append(ObjectivesApplicable).AppendLine();
            text.Append("Hazards found: ").AppendLine(Names(HazardsFound));
            text.Append("Hazards missed: ").AppendLine(Names(HazardsMissed));
            text.Append("Decision: ").Append(Decision).AppendLine(DecisionWasImplicit ? " (implicit)" : string.Empty);
            text.Append("Practice: ").AppendLine(Practice.ToString());
            text.Append("Reached assembly point: ").AppendLine(ReachedSafeZone ? "yes" : "no");
            text.Append("Unsafe actions: ").Append(UnsafeActionCount).AppendLine();
            foreach (ObjectiveResult objective in Objectives)
            {
                text.Append(" [").Append(Symbol(objective.Status)).Append("] ").Append(objective.Title)
                    .Append(" - ").AppendLine(objective.Feedback);
                text.Append("      criterion: ").AppendLine(objective.Criterion);
            }
            if (includeEventLog)
            {
                text.AppendLine("--- Event log ---");
                foreach (SessionEvent entry in Events)
                    text.AppendLine(entry.ToString());
            }
            text.Append("Note: ").AppendLine(Disclaimer);
            return text.ToString();
        }

        private static string Symbol(ObjectiveStatus status) =>
            status == ObjectiveStatus.Achieved ? "x" : status == ObjectiveStatus.Missed ? " " : "-";

        private static string Names(IReadOnlyList<HazardInfo> hazards)
        {
            if (hazards.Count == 0)
                return "none";
            var names = new string[hazards.Count];
            for (int i = 0; i < hazards.Count; i++)
                names[i] = hazards[i].DisplayName;
            return string.Join(", ", names);
        }
    }
}
