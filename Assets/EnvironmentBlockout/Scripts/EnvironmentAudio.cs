using UnityEngine;

namespace OfficeTraining.Environment
{
    /// <summary>Spatial prototype sounds; not a representation of an official/local alarm signal.</summary>
    public sealed class EnvironmentAudio : MonoBehaviour
    {
        [SerializeField] private IncidentController incident;
        [SerializeField] private AudioSource fireLoop;
        [SerializeField] private AudioSource alarmLoop;

        private bool firePaused;
        private bool alarmPaused;

        private void OnEnable()
        {
            if (incident == null)
                return;
            incident.OnIncidentStateChanged += HandleState;
            incident.OnPauseChanged += HandlePause;
            incident.OnIncidentReset += StopAudio;
            Refresh();
        }

        private void OnDisable()
        {
            if (incident != null)
            {
                incident.OnIncidentStateChanged -= HandleState;
                incident.OnPauseChanged -= HandlePause;
                incident.OnIncidentReset -= StopAudio;
            }
            StopAudio();
        }

        private void HandleState(IncidentState state) => Refresh();
        private void HandlePause(bool paused) => Refresh();

        private void Refresh()
        {
            bool active = incident.CurrentState == IncidentState.Active || incident.CurrentState == IncidentState.EvacuationRequired;
            RefreshSource(fireLoop, active, incident.IsPaused, ref firePaused);
            RefreshSource(alarmLoop, active, incident.IsPaused, ref alarmPaused);
        }

        private static void RefreshSource(AudioSource source, bool active, bool paused, ref bool wasPaused)
        {
            if (source == null)
                return;
            if (!active)
            {
                source.Stop();
                wasPaused = false;
            }
            else if (paused)
            {
                if (wasPaused)
                    return;
                if (!source.isPlaying)
                    source.Play();
                source.Pause();
                wasPaused = true;
            }
            else
            {
                if (wasPaused)
                    source.UnPause();
                if (!source.isPlaying)
                    source.Play();
                wasPaused = false;
            }
        }

        private void StopAudio()
        {
            if (fireLoop != null)
                fireLoop.Stop();
            if (alarmLoop != null)
                alarmLoop.Stop();
            firePaused = alarmPaused = false;
        }
    }
}
