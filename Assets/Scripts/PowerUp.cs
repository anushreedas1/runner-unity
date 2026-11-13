using UnityEngine;

public enum PowerUpType
{
    Magnet,
    Shield,
    DoubleScore
}

public class PowerUp : MonoBehaviour
{
    public PowerUpType Type;

    [SerializeField] private float spinSpeed = 120f;
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 3f;

    private float baseY;

    void Start()
    {
        baseY = transform.localPosition.y;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

        Vector3 p = transform.localPosition;
        p.y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.localPosition = p;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerRunner>() == null) return;

        if (GameManager.Instance != null) GameManager.Instance.ActivatePowerUp(Type);
        SoundManager.Play(SoundManager.Sfx.PowerUp);
        Destroy(gameObject);
    }
}