using UnityEngine;

namespace OfficeTraining.Environment
{
    /// <summary>Lightweight presentation only. The coordinator owns every incident decision.</summary>
    public sealed class FireScenarioVisuals : MonoBehaviour
    {
        [SerializeField] private IncidentController incident;
        [SerializeField] private ParticleSystem[] effects = System.Array.Empty<ParticleSystem>();

        private void OnEnable()
        {
            if (incident == null)
                return;
            incident.OnIncidentStateChanged += HandleState;
            incident.OnPauseChanged += HandlePause;
            incident.OnIncidentReset += ClearEffects;
            Refresh();
        }

        private void OnDisable()
        {
            if (incident != null)
            {
                incident.OnIncidentStateChanged -= HandleState;
                incident.OnPauseChanged -= HandlePause;
                incident.OnIncidentReset -= ClearEffects;
            }
            ClearEffects();
        }

        private void HandleState(IncidentState state) => Refresh();
        private void HandlePause(bool paused) => Refresh();

        private void Refresh()
        {
            bool active = incident.CurrentState == IncidentState.Active || incident.CurrentState == IncidentState.EvacuationRequired;
            foreach (ParticleSystem effect in effects)
            {
                if (effect == null)
                    continue;
                if (!active)
                {
                    effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    continue;
                }
                // Play also resumes an already-paused system; don't restart a running one.
                if (!effect.isPlaying)
                    effect.Play(false);
                if (incident.IsPaused)
                    effect.Pause(false);
            }
        }

        private void ClearEffects()
        {
            foreach (ParticleSystem effect in effects)
                if (effect != null)
                    effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
