using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private Transform player;

    [Header("Chase distances (behind the player)")]
    [SerializeField] private float farDistance = 4.5f;
    [SerializeField] private float closeDistance = 2.2f;
    [SerializeField] private float catchDistance = 1.4f;

    [Header("Speeds")]
    [SerializeField] private float approachSpeed = 6f;
    [SerializeField] private float sideFollowSpeed = 8f;

    private float currentDistance;
    private Material templateMaterial;

    void Start()
    {
        currentDistance = farDistance;

        // Copy the player's material, because that one works in a phone build
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

        Vector3 pos = transform.position;
        pos.z = player.position.z - currentDistance;
        pos.x = Mathf.Lerp(pos.x, player.position.x, sideFollowSpeed * Time.deltaTime);
        pos.y = 1.2f + Mathf.Sin(Time.time * 12f) * 0.1f; // little running bob
        transform.position = pos;
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