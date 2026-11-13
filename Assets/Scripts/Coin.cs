using UnityEngine;

public class Coin : MonoBehaviour
{
    [SerializeField] private float spinSpeed = 180f;
    [SerializeField] private float magnetRadius = 8f;
    [SerializeField] private float magnetPullSpeed = 30f;

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.MagnetActive || gm.PlayerTransform == null) return;

        Vector3 target = gm.PlayerTransform.position;
        if ((target - transform.position).sqrMagnitude <= magnetRadius * magnetRadius)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, magnetPullSpeed * Time.deltaTime);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerRunner>() == null) return;

        if (GameManager.Instance != null) GameManager.Instance.AddCoin();
        SoundManager.Play(SoundManager.Sfx.Coin);
        Destroy(gameObject);
    }
}