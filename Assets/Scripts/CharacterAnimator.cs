using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Plays a character's animation clips directly, so no Animator Controller is needed.
// Put this on the character model (the object that has the Animator) and drag the clips in.
[RequireComponent(typeof(Animator))]
public class CharacterAnimator : MonoBehaviour
{
    public enum State
    {
        Idle,
        Run,
        Jump,
        Slide,
        Stumble,
        Death
    }

    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip runClip;
    [SerializeField] private AnimationClip jumpClip;
    [SerializeField] private AnimationClip slideClip;
    [SerializeField] private AnimationClip stumbleClip;
    [SerializeField] private AnimationClip deathClip;
    [SerializeField] private float blendTime = 0.12f;

    private const int StateCount = 6;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private bool graphCreated;
    private bool built;

    private readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[StateCount];
    private readonly int[] inputIndex = new int[StateCount];
    private readonly float[] lengths = new float[StateCount];
    private readonly float[] times = new float[StateCount];
    private readonly float[] weights = new float[StateCount];
    private readonly float[] speeds = new float[StateCount];

    private State current = State.Idle;
    private float runSpeedMultiplier = 1f;

    void Awake()
    {
        Build();
    }

    void OnDestroy()
    {
        if (graphCreated && graph.IsValid()) graph.Destroy();
    }

    // Faster running animation when the game speeds up, so the feet don't slide
    public void SetRunSpeed(float multiplier)
    {
        runSpeedMultiplier = multiplier;
    }

    // Starts a state. If duration is given, a one-shot clip is stretched to last that long.
    public void Play(State state, float duration = 0f)
    {
        Build();
        if (!graphCreated) return;

        // No idle clip? Stand in with the run clip
        if (inputIndex[(int)state] < 0)
        {
            if (state == State.Idle && inputIndex[(int)State.Run] >= 0) state = State.Run;
            else return;
        }

        int i = (int)state;
        times[i] = 0f;
        speeds[i] = duration > 0f ? lengths[i] / duration : 1f;
        current = state;
    }

    private void Build()
    {
        if (built) return;
        built = true;

        Animator animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = null;

        AnimationClip[] clips = { idleClip, runClip, jumpClip, slideClip, stumbleClip, deathClip };

        int count = 0;
        for (int i = 0; i < StateCount; i++)
        {
            inputIndex[i] = -1;
            if (clips[i] == null) continue;

            inputIndex[i] = count;
            count++;
        }

        if (count == 0) return;

        graph = PlayableGraph.Create("CharacterAnimator " + gameObject.name);
        graphCreated = true;
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        mixer = AnimationMixerPlayable.Create(graph, count);
        output.SetSourcePlayable(mixer);

        for (int i = 0; i < StateCount; i++)
        {
            if (clips[i] == null) continue;

            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clips[i]);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetSpeed(0.0); // the time of each clip is set by hand in Update

            graph.Connect(clipPlayable, 0, mixer, inputIndex[i]);
            mixer.SetInputWeight(inputIndex[i], 0f);

            playables[i] = clipPlayable;
            lengths[i] = Mathf.Max(0.05f, clips[i].length);
            speeds[i] = 1f;
        }

        // Start on idle (or run, if there is no idle clip)
        current = inputIndex[(int)State.Idle] >= 0 ? State.Idle : State.Run;
        weights[(int)current] = 1f;

        graph.Play();
    }

    private static bool IsLooping(State state)
    {
        return state == State.Idle || state == State.Run;
    }

    void Update()
    {
        if (!graphCreated) return;

        float dt = Time.deltaTime;

        for (int i = 0; i < StateCount; i++)
        {
            if (inputIndex[i] < 0) continue;

            // Fade the current clip in and the others out
            float target = (i == (int)current) ? 1f : 0f;
            weights[i] = blendTime > 0f ? Mathf.MoveTowards(weights[i], target, dt / blendTime) : target;

            if (weights[i] <= 0f) continue;

            float speed = speeds[i];
            if (i == (int)State.Run) speed *= runSpeedMultiplier;

            times[i] += dt * speed;

            if (IsLooping((State)i)) times[i] %= lengths[i];
            else times[i] = Mathf.Min(times[i], lengths[i]);

            playables[i].SetTime(times[i]);
        }

        // Keep the blend weights adding up to one
        float total = 0f;
        for (int i = 0; i < StateCount; i++)
        {
            if (inputIndex[i] >= 0) total += weights[i];
        }

        if (total > 0f)
        {
            for (int i = 0; i < StateCount; i++)
            {
                if (inputIndex[i] >= 0) mixer.SetInputWeight(inputIndex[i], weights[i] / total);
            }
        }

        // After a stumble, get back to running
        int stumble = (int)State.Stumble;
        if (current == State.Stumble && inputIndex[stumble] >= 0 && times[stumble] >= lengths[stumble] - 0.001f)
        {
            Play(State.Run);
        }
    }
}