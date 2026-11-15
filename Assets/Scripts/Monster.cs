using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private Transform player;

    [Header("Character model (optional)")]
    [SerializeField] private CharacterAnimator model;
    [SerializeField] private float modelHeight = 1.2f;

    [Header("Chase distances (behind the player)")]
    [SerializeField] private float farDistance = 4.5f;
    [SerializeField] private float closeDistance = 2.2f;
    [SerializeField] private float catchDistance = 1.4f;

    [Header("Speeds")]
    [SerializeField] private float approachSpeed = 6f;
    [SerializeField] private float sideFollowSpeed = 8f;

    private float currentDistance;
    private Material templateMaterial;
    private bool modelIdle;

    void Start()
    {
        currentDistance = farDistance;

        if (model != null)
        {
            // Feet on the ground, facing forward
            model.transform.localPosition = new Vector3(0f, -modelHeight, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.Play(CharacterAnimator.State.Run);
            return;
        }

        // No model: copy the player's material, because that one works in a phone build
        if (player != null)
        {
            Renderer playerRenderer = player.GetComponent<Renderer>();
            if (playerRenderer != null) templateMaterial = playerRenderer.sharedMaterial;
        }

        BuildVisual();
    }

    void Update()
    {
        if (player == null) return;

        GameManager gm = GameManager.Instance;

        float targetDistance = farDistance;
        if (gm != null)
        {
            if (gm.IsGameOver) targetDistance = catchDistance;
            else if (gm.IsStumbled) targetDistance = closeDistance;
        }

        currentDistance = Mathf.MoveTowards(currentDistance, targetDistance, approachSpeed * Time.deltaTime);

        // A little running bob for the simple shape (a model already bobs by itself)
        float bob = model == null ? Mathf.Sin(Time.time * 12f) * 0.1f : 0f;

        Vector3 pos = transform.position;
        pos.z = player.position.z - currentDistance;
        pos.x = Mathf.Lerp(pos.x, player.position.x, sideFollowSpeed * Time.deltaTime);
        pos.y = modelHeight + bob;
        transform.position = pos;

        AnimateModel(gm);
    }

    private void AnimateModel(GameManager gm)
    {
        if (model == null || gm == null) return;

        bool shouldIdle = gm.IsGameOver || !gm.IsPlaying;

        if (shouldIdle != modelIdle)
        {
            model.Play(shouldIdle ? CharacterAnimator.State.Idle : CharacterAnimator.State.Run);
            modelIdle = shouldIdle;
        }

        model.SetRunSpeed(Mathf.Clamp(gm.CurrentSpeed / 9f, 0.8f, 1.8f));
    }

    private void BuildVisual()
    {
        CreatePart(PrimitiveType.Capsule, Vector3.zero, new Vector3(1.6f, 1.2f, 1.6f), new Color(0.55f, 0.05f, 0.05f));
        CreatePart(PrimitiveType.Sphere, new Vector3(-0.35f, 0.6f, 0.7f), Vector3.one * 0.3f, new Color(1f, 0.9f, 0.1f));
        CreatePart(PrimitiveType.Sphere, new Vector3(0.35f, 0.6f, 0.7f), Vector3.one * 0.3f, new Color(1f, 0.9f, 0.1f));
    }

    private void CreatePart(PrimitiveType type, Vector3 localPosition, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;

        Renderer partRenderer = part.GetComponent<Renderer>();
        if (templateMaterial != null)
        {
            Material material = new Material(templateMaterial);
            material.color = color;
            partRenderer.material = material;
        }
        else
        {
            partRenderer.material.color = color;
        }

        Destroy(part.GetComponent<Collider>());
    }
}