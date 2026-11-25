using System;
using System.Collections.Generic;
using System.Globalization;

namespace OfficeTraining.Scenario
{
    /// <summary>
    /// Turns the session state into objective results with transparent criteria. Pure function:
    /// it reads the state machine and never changes it. There is deliberately no numeric score.
    /// </summary>
    public static class AssessmentService
    {
        public static class ObjectiveIds
        {
            public const string Hazards = "hazards";
            public const string Decision = "safe_decision";
            public const string Practice = "practice";
            public const string Evacuation = "evacuation";
        }

        public static TrainingReport Evaluate(TrainingStateMachine session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            ScenarioRules rules = session.Rules;
            var found = new List<HazardInfo>();
            foreach (string id in session.ConfirmedHazards)
                found.Add(new HazardInfo(id, rules.DisplayNameFor(id)));
            var missed = new List<HazardInfo>();
            foreach (HazardInfo hazard in rules.Hazards)
                if (!Contains(session.ConfirmedHazards, hazard.Id))
                    missed.Add(hazard);

            var objectives = new List<ObjectiveResult>
            {
                EvaluateHazards(rules, found, missed),
                EvaluateDecision(session),
                EvaluatePractice(session),
                EvaluateEvacuation(session)
            };

            return new TrainingReport(session.IsCompleted, session.EndedEarly, session.ElapsedSeconds,
                found.AsReadOnly(), missed.AsReadOnly(), rules.RequiredHazardCount, session.Decision,
                session.DecisionWasImplicit, session.Practice, session.ReachedSafeZone, session.UnsafeActionCount,
                objectives.AsReadOnly(), new List<SessionEvent>(session.Events).AsReadOnly());
        }

        private static ObjectiveResult EvaluateHazards(ScenarioRules rules, List<HazardInfo> found, List<HazardInfo> missed)
        {
            int total = rules.Hazards.Count;
            int required = rules.RequiredHazardCount;
            string criterion = Format(FeedbackText.HazardsCriterion, required, total);
            if (found.Count < required)
                return Objective(ObjectiveIds.Hazards, FeedbackText.HazardsTitle, criterion, ObjectiveStatus.Missed,
                    Format(FeedbackText.HazardsTooFew, found.Count, total, required, Names(missed)));

            string feedback = total == 0 ? FeedbackText.HazardsNone
                : missed.Count == 0 ? Format(FeedbackText.HazardsAllFound, total)
                : Format(FeedbackText.HazardsEnough, found.Count, total, Names(missed));
            ObjectiveStatus status = required == 0 ? ObjectiveStatus.NotApplicable : ObjectiveStatus.Achieved;
            return Objective(ObjectiveIds.Hazards, FeedbackText.HazardsTitle, criterion, status, feedback);
        }

        private static ObjectiveResult EvaluateDecision(TrainingStateMachine session)
        {
            ObjectiveStatus status;
            string feedback;
            switch (session.Decision)
            {
                case DecisionPath.None:
                    status = ObjectiveStatus.Missed;
                    feedback = FeedbackText.DecisionNone;
                    break;
                case DecisionPath.PracticeWhenNotPermitted:
                    status = ObjectiveStatus.Missed;
                    feedback = FeedbackText.DecisionNotPermitted;
                    break;
                default:
                    // UnsafeActionCount only contains extinguisher misuse on these paths.
                    if (session.UnsafeActionCount > 0)
                    {
                        status = ObjectiveStatus.Missed;
                        feedback = Format(FeedbackText.DecisionUnsafeUse, session.UnsafeActionCount);
                    }
                    else
                    {
                        status = ObjectiveStatus.Achieved;
                        feedback = session.Decision == DecisionPath.PracticeInPermittedCondition
                            ? FeedbackText.DecisionPracticePermitted
                            : session.DecisionWasImplicit ? FeedbackText.DecisionEvacuatedImplicit : FeedbackText.DecisionEvacuated;
                    }
                    break;
            }
            return Objective(ObjectiveIds.Decision, FeedbackText.DecisionTitle, FeedbackText.DecisionCriterion, status, feedback);
        }

        private static ObjectiveResult EvaluatePractice(TrainingStateMachine session)
        {
            if (session.Decision != DecisionPath.PracticeInPermittedCondition)
                return Objective(ObjectiveIds.Practice, FeedbackText.PracticeTitle, FeedbackText.PracticeCriterion,
                    ObjectiveStatus.NotApplicable, FeedbackText.PracticeNotApplicable);

            switch (session.Practice)
            {
                case PracticeResult.Succeeded:
                    return Objective(ObjectiveIds.Practice, FeedbackText.PracticeTitle, FeedbackText.PracticeCriterion,
                        ObjectiveStatus.Achieved, FeedbackText.PracticeSucceeded);
                case PracticeResult.Failed:
                    return Objective(ObjectiveIds.Practice, FeedbackText.PracticeTitle, FeedbackText.PracticeCriterion,
                        ObjectiveStatus.Missed, FeedbackText.PracticeFailed);
                case PracticeResult.Abandoned:
                    return Objective(ObjectiveIds.Practice, FeedbackText.PracticeTitle, FeedbackText.PracticeCriterion,
                        ObjectiveStatus.Missed, FeedbackText.PracticeAbandoned);
                default:
                    return Objective(ObjectiveIds.Practice, FeedbackText.PracticeTitle, FeedbackText.PracticeCriterion,
                        ObjectiveStatus.Missed, FeedbackText.PracticeNotFinished);
            }
        }

        private static ObjectiveResult EvaluateEvacuation(TrainingStateMachine session) =>
            Objective(ObjectiveIds.Evacuation, FeedbackText.EvacuationTitle, FeedbackText.EvacuationCriterion,
                session.ReachedSafeZone ? ObjectiveStatus.Achieved : ObjectiveStatus.Missed,
                session.ReachedSafeZone ? FeedbackText.EvacuationReached : FeedbackText.EvacuationMissed);

        private static ObjectiveResult Objective(string id, string title, string criterion, ObjectiveStatus status, string feedback) =>
            new ObjectiveResult(id, title, criterion, status, feedback);

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            foreach (string candidate in ids)
                if (string.Equals(candidate, id, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string Names(List<HazardInfo> hazards)
        {
            var names = new string[hazards.Count];
            for (int i = 0; i < hazards.Count; i++)
                names[i] = hazards[i].DisplayName;
            return string.Join(", ", names);
        }

        private static string Format(string template, params object[] args) =>
            string.Format(CultureInfo.InvariantCulture, template, args);
    }
}
