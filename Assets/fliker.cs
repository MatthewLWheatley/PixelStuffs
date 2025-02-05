using UnityEngine;

public class flicker : MonoBehaviour
{
    public Light flameLight;  // Assign your Light component in the Inspector
    public float minIntensity = 0.8f;
    public float maxIntensity = 1.2f;
    public float flickerSpeed = 0.1f; // Adjust how fast the flickering happens

    private float targetIntensity;
    private float timer;

    void Start()
    {
        if (flameLight == null)
        {
            flameLight = GetComponent<Light>();
        }
        SetNewIntensity();
    }

    void Update()
    {
        timer -= Time.deltaTime;

        // Gradually change intensity
        flameLight.intensity = Mathf.Lerp(flameLight.intensity, targetIntensity, Time.deltaTime * 10f);

        // Pick a new random intensity at random intervals
        if (timer <= 0f)
        {
            SetNewIntensity();
        }
    }

    void SetNewIntensity()
    {
        targetIntensity = Random.Range(minIntensity, maxIntensity);
        timer = Random.Range(0.05f, flickerSpeed);
    }
}
