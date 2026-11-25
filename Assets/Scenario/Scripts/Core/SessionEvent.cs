using System.Globalization;
using System.Text;

namespace OfficeTraining.Scenario
{
    public enum SessionEventType
    {
        TrainingStarted,
        PhaseChanged,
        HazardConfirmed,
        InspectionFinished,
        IncidentAcknowledged,
        EvacuationChosen,
        PracticeChosen,
        ItemGrabbed,
        ItemReleased,
        ItemUsed,
        PracticeResultReported,
        SafeZoneReached,
        Paused,
        Resumed,
        TrainingEnded
    }

    /// <summary>What the state machine did with an incoming event. Rejected events are logged too.</summary>
    public enum EventOutcome
    {
        Accepted,
        Duplicate,
        WrongPhase,
        WhilePaused,
        UnknownId,
        NotPermitted
    }

    /// <summary>One immutable entry in the session log.</summary>
    public sealed class SessionEvent
    {
        public SessionEvent(int sequence, double timeSeconds, SessionEventType type, TrainingPhase phase,
            EventOutcome outcome, string subject, string detail)
        {
            Sequence = sequence;
            TimeSeconds = timeSeconds;
            Type = type;
            Phase = phase;
            Outcome = outcome;
            Subject = subject ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        /// <summary>1-based order within the current session.</summary>
        public int Sequence { get; }
        /// <summary>Seconds since the training was started (or since reset, before starting).</summary>
        public double TimeSeconds { get; }
        public SessionEventType Type { get; }
        /// <summary>Phase when the event was received (the new phase for PhaseChanged).</summary>
        public TrainingPhase Phase { get; }
        public EventOutcome Outcome { get; }
        /// <summary>Hazard id, item id or phase name, depending on the type.</summary>
        public string Subject { get; }
        public string Detail { get; }
        public bool Accepted => Outcome == EventOutcome.Accepted;

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append('#').Append(Sequence.ToString("000", CultureInfo.InvariantCulture));
            text.Append(' ').Append(TimeSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append("s");
            text.Append(" [").Append(Phase).Append("] ").Append(Type);
            if (Subject.Length > 0)
                text.Append('(').Append(Subject).Append(')');
            text.Append(" -> ").Append(Outcome);
            if (Detail.Length > 0)
                text.Append(": ").Append(Detail);
            return text.ToString();
        }
    }
}
