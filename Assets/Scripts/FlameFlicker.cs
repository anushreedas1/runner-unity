using UnityEngine;

// Makes a group of flame pieces (and an optional light) flicker like a real fire.
// Put it on an object whose children are the flame pieces.
public class FlameFlicker : MonoBehaviour
{
    [SerializeField] private float speed = 7f;

    private Transform[] pieces;
    private Vector3[] baseScales;
    private float[] offsets;

    private Light fireLight;
    private float lightBaseIntensity;

    void Start()
    {
        int count = transform.childCount;
        pieces = new Transform[count];
        baseScales = new Vector3[count];
        offsets = new float[count];

        for (int i = 0; i < count; i++)
        {
            pieces[i] = transform.GetChild(i);
            baseScales[i] = pieces[i].localScale;
            offsets[i] = Random.Range(0f, 100f);
        }

        // A fire blower has a light next to its flames, so flicker that too
        if (transform.parent != null)
        {
            fireLight = transform.parent.GetComponentInChildren<Light>();
            if (fireLight != null) lightBaseIntensity = fireLight.intensity;
        }
    }

    void Update()
    {
        if (pieces == null) return;

        for (int i = 0; i < pieces.Length; i++)
        {
            float n = Mathf.PerlinNoise(Time.time * speed, offsets[i]);
            Vector3 b = baseScales[i];

            pieces[i].localScale = new Vector3(
                b.x * (0.88f + 0.24f * n),
                b.y * (0.70f + 0.65f * n),
                b.z * (0.88f + 0.24f * n));
        }

        if (fireLight != null)
        {
            float n = Mathf.PerlinNoise(Time.time * speed * 0.8f, 7.3f);
            fireLight.intensity = lightBaseIntensity * (0.75f + 0.5f * n);
        }
    }
}