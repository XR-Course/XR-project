using UnityEngine;

namespace OfficeTraining.Interaction
{
    /// <summary>
    /// Simplified extinguisher practice on a grabbable prop: while the trigger is held, a ray from the
    /// nozzle checks whether it points at a <see cref="PracticeTarget"/>. Aim time is counted by a
    /// <see cref="PracticeAimTracker"/> only while the practice is armed (the scenario's Practice
    /// phase). The result is published on the InteractionEvents channel; this component never
    /// decides whether practice was a safe choice. No fluid, fire or combustion simulation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ToolInteractionController))]
    public sealed class ExtinguisherPractice : MonoBehaviour
    {
        [SerializeField] private InteractionEvents events;
        [Tooltip("Spray origin; forward is the spray direction. Defaults to the tool's use point.")]
        [SerializeField] private Transform nozzle;
        [SerializeField] private float aimRange = 4f;
        [SerializeField] private LayerMask aimMask = ~0;
        [Tooltip("Seconds of spraying at the target needed for a successful practice.")]
        [SerializeField] private float requiredAimSeconds = 2.5f;
        [Tooltip("Spraying this long without success counts as a failed attempt. 0 = no limit.")]
        [SerializeField] private float maxSpraySeconds = 0f;
        [Tooltip("Arm on start (stand-alone test scenes). In the office the scenario bridge arms it in the Practice phase.")]
        [SerializeField] private bool armedOnStart;

        [Header("Feedback (optional)")]
        [SerializeField] private ParticleSystem sprayEffect;
        [SerializeField] private AudioSource sprayAudio;
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private Color aimLineOffTarget = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color aimLineOnTarget = new Color(0.2f, 1f, 0.3f, 0.9f);

        private ToolInteractionController tool;
        private PracticeAimTracker tracker;
        private PracticeTarget currentTarget;
        private float lastPublishedProgress = -1f;

        public PracticeAimTracker Tracker { get { EnsureTracker(); return tracker; } }
        public bool Armed => Tracker.Armed;
        public bool OnTarget => currentTarget != null;
        public float Progress => Tracker.Progress;
        public PracticeOutcome Outcome => Tracker.Outcome;
        public InteractionEvents Events { get => events; set => events = value; }

        private void Awake()
        {
            tool = GetComponent<ToolInteractionController>();
            if (events == null)
                events = tool.Events;
            EnsureTracker();
            tracker.SetArmed(armedOnStart);
            SetSprayFeedback(false);
        }

        private void OnEnable()
        {
            tool.UseStarted += HandleUseStarted;
            tool.UseStopped += HandleUseStopped;
        }

        private void OnDisable()
        {
            tool.UseStarted -= HandleUseStarted;
            tool.UseStopped -= HandleUseStopped;
            SetCurrentTarget(null);
            SetSprayFeedback(false);
        }

        /// <summary>Allow aim time to count (Practice phase, not paused).</summary>
        public void SetArmed(bool armed)
        {
            Tracker.SetArmed(armed);
            if (!armed)
                SetCurrentTarget(null);
        }

        /// <summary>Clears progress, result and feedback for a new attempt (session reset).</summary>
        public void ResetPractice()
        {
            Tracker.Reset();
            lastPublishedProgress = -1f;
            SetCurrentTarget(null);
            SetSprayFeedback(tool != null && tool.IsUsing);
            foreach (PracticeTarget target in FindObjectsByType<PracticeTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                target.ResetTarget();
        }

        private void Update()
        {
            bool spraying = tool.IsHeld && tool.IsUsing;
            PracticeTarget hitTarget = spraying ? FindAimedTarget() : null;
            SetCurrentTarget(hitTarget);
            UpdateAimLine(spraying);

            if (tracker.Tick(Time.deltaTime, spraying, hitTarget != null))
            {
                bool success = tracker.Outcome == PracticeOutcome.Succeeded;
                if (success && hitTarget != null)
                    hitTarget.MarkCompleted();
                PublishProgress();
                events?.RaisePracticeResult(tool.ItemId, success);
                return;
            }
            PublishProgress();
        }

        /// <summary>
        /// Nearest practice target along the spray ray. The prop itself, other triggers and inspect
        /// colliders (hazard selection volumes) do not block the spray; walls and furniture do.
        /// </summary>
        private PracticeTarget FindAimedTarget()
        {
            Transform origin = nozzle != null ? nozzle : tool.UsePoint;
            int count = Physics.RaycastNonAlloc(origin.position, origin.forward, hitBuffer, aimRange, aimMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hitBuffer, 0, count, HitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = hitBuffer[i].collider;
                if (hitCollider.transform.IsChildOf(transform))
                    continue;
                PracticeTarget target = hitCollider.GetComponentInParent<PracticeTarget>();
                if (target != null)
                    return target;
                if (hitCollider.isTrigger || hitCollider.GetComponentInParent<InspectTarget>() != null)
                    continue;
                return null;
            }
            return null;
        }

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        private void SetCurrentTarget(PracticeTarget target)
        {
            if (currentTarget == target)
                return;
            if (currentTarget != null)
                currentTarget.SetAimed(false);
            currentTarget = target;
            if (currentTarget != null)
                currentTarget.SetAimed(true);
        }

        private void PublishProgress()
        {
            float progress = tracker.Progress;
            if (Mathf.Approximately(progress, lastPublishedProgress))
                return;
            lastPublishedProgress = progress;
            events?.RaisePracticeProgress(tool.ItemId, progress);
        }

        private void UpdateAimLine(bool spraying)
        {
            if (aimLine == null)
                return;
            aimLine.enabled = spraying;
            if (!spraying)
                return;
            Transform origin = nozzle != null ? nozzle : tool.UsePoint;
            aimLine.useWorldSpace = true;
            aimLine.positionCount = 2;
            aimLine.SetPosition(0, origin.position);
            aimLine.SetPosition(1, origin.position + origin.forward * aimRange);
            Color color = currentTarget != null ? aimLineOnTarget : aimLineOffTarget;
            aimLine.startColor = color;
            aimLine.endColor = color;
        }

        private void HandleUseStarted() => SetSprayFeedback(true);

        private void HandleUseStopped()
        {
            SetSprayFeedback(false);
            SetCurrentTarget(null);
            if (aimLine != null)
                aimLine.enabled = false;
        }

        private void SetSprayFeedback(bool on)
        {
            if (sprayEffect != null)
            {
                if (on && !sprayEffect.isPlaying)
                    sprayEffect.Play(true);
                else if (!on && sprayEffect.isPlaying)
                    sprayEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (sprayAudio != null)
            {
                if (on && !sprayAudio.isPlaying)
                    sprayAudio.Play();
                else if (!on && sprayAudio.isPlaying)
                    sprayAudio.Stop();
            }
        }

        private void EnsureTracker()
        {
            if (tracker != null)
                return;
            float required = Mathf.Max(0.1f, requiredAimSeconds);
            float max = maxSpraySeconds <= 0f ? 0f : Mathf.Max(required, maxSpraySeconds);
            tracker = new PracticeAimTracker(required, max);
        }
    }
}
