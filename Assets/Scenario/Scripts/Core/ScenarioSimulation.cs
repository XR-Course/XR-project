using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace OfficeTraining.Scenario
{
    /// <summary>Clock that only moves when told to, for deterministic tests and simulations.</summary>
    public sealed class ManualClock
    {
        public double Now { get; private set; }
        public void Advance(double seconds) => Now += seconds;
        public Func<double> AsFunc() => () => Now;
    }

    /// <summary>
    /// Scripted mock sessions that drive the state machine with the same calls the real modules
    /// make (environment, interaction, UI). Used by the editor menu, the console runner and tests.
    /// </summary>
    public static class ScenarioSimulation
    {
        public sealed class Script
        {
            public Script(string name, bool practicePermitted, Action<TrainingStateMachine, ManualClock> steps)
            {
                Name = name;
                PracticePermitted = practicePermitted;
                Steps = steps;
            }

            public string Name { get; }
            public bool PracticePermitted { get; }
            public Action<TrainingStateMachine, ManualClock> Steps { get; }
        }

        public static IReadOnlyList<Script> Scripts { get; } = new ReadOnlyCollection<Script>(new[]
        {
            new Script("Normal completion with permitted practice", true, (s, clock) =>
            {
                s.StartTraining();
                clock.Advance(20); s.ConfirmHazard("power_strip");
                clock.Advance(15); s.ConfirmHazard("heater_papers");
                clock.Advance(25); s.ConfirmHazard("damaged_cable"); // all found: incident starts
                clock.Advance(5); s.AcknowledgeIncident();
                clock.Advance(5); s.NotifyGrabbed(ScenarioRules.DefaultExtinguisherItemId); // chooses practice
                clock.Advance(4); s.NotifyUsed(ScenarioRules.DefaultExtinguisherItemId);
                clock.Advance(6); s.ReportPracticeResult(true);
                s.NotifyReleased(ScenarioRules.DefaultExtinguisherItemId);
                clock.Advance(30); s.ReachSafeZone();
            }),
            new Script("Evacuation first, one hazard missed", true, (s, clock) =>
            {
                s.StartTraining();
                clock.Advance(30); s.ConfirmHazard("heater_papers");
                clock.Advance(30); s.ConfirmHazard("power_strip");
                clock.Advance(10); s.FinishInspection();
                clock.Advance(5); s.AcknowledgeIncident();
                clock.Advance(3); s.ChooseEvacuate();
                clock.Advance(25); s.ReachSafeZone();
            }),
            new Script("Unsafe: extinguisher used when practice is not permitted", false, (s, clock) =>
            {
                s.StartTraining();
                clock.Advance(20); s.ConfirmHazard("power_strip");
                clock.Advance(20); s.ConfirmHazard("heater_papers");
                clock.Advance(20); s.ConfirmHazard("damaged_cable");
                clock.Advance(5); s.NotifyUsed(ScenarioRules.DefaultExtinguisherItemId); // unsafe use
                clock.Advance(2); s.ChoosePractice(); // not permitted: moves to evacuation
                clock.Advance(30); s.ReachSafeZone();
            }),
            new Script("Invalid order, duplicates and pause", true, (s, clock) =>
            {
                s.ReachSafeZone();                       // before start
                s.ConfirmHazard("power_strip");          // before start
                s.StartTraining();
                s.StartTraining();                       // duplicate start
                clock.Advance(10); s.ConfirmHazard("power_strip");
                s.ConfirmHazard("power_strip");          // duplicate
                s.ConfirmHazard("fire_door");            // unknown id
                s.ReportPracticeResult(true);            // not in practice: not rewarded
                s.Pause();
                s.ConfirmHazard("heater_papers");        // rejected while paused
                clock.Advance(60);
                s.Resume();
                s.ConfirmHazard("heater_papers");
                s.FinishInspection();
                clock.Advance(20); s.ReachSafeZone();    // straight to the exit = evacuate
                s.ReachSafeZone();                       // duplicate arrival
            }),
            new Script("Reset mid-session, then quick evacuation", true, (s, clock) =>
            {
                s.StartTraining();
                clock.Advance(10); s.ConfirmHazard("power_strip");
                s.Pause();
                s.Reset();                               // fresh session
                s.StartTraining();
                clock.Advance(15); s.FinishInspection();
                clock.Advance(3); s.ChooseEvacuate();
                clock.Advance(20); s.ReachSafeZone();
            }),
            new Script("Ended early from the pause menu", true, (s, clock) =>
            {
                s.StartTraining();
                clock.Advance(40); s.ConfirmHazard("damaged_cable");
                s.Pause();
                s.EndTraining();
            })
        });

        public static TrainingReport Run(Script script)
        {
            if (script == null)
                throw new ArgumentNullException(nameof(script));
            var clock = new ManualClock();
            var session = new TrainingStateMachine(ScenarioRules.CreateDefault(script.PracticePermitted), clock.AsFunc());
            script.Steps(session, clock);
            return session.BuildReport();
        }

        public static string RunAllAsText(bool includeEventLogs = true)
        {
            var text = new StringBuilder();
            foreach (Script script in Scripts)
            {
                text.Append("##### ").Append(script.Name)
                    .Append(script.PracticePermitted ? " (practice permitted)" : " (practice not permitted)").AppendLine();
                text.AppendLine(Run(script).ToText(includeEventLogs));
            }
            return text.ToString();
        }
    }
}
