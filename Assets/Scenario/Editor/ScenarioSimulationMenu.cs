using UnityEditor;
using UnityEngine;

namespace OfficeTraining.Scenario.Editor
{
    /// <summary>Runs the scripted mock sessions without a headset or scene and logs each report.</summary>
    public static class ScenarioSimulationMenu
    {
        [MenuItem("Office Training/Scenario/Run Mock Simulations")]
        public static void RunAll()
        {
            foreach (ScenarioSimulation.Script script in ScenarioSimulation.Scripts)
            {
                TrainingReport report = ScenarioSimulation.Run(script);
                Debug.Log($"[Scenario simulation] {script.Name}\n{report.ToText(includeEventLog: true)}");
            }
        }
    }
}
