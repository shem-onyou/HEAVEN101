using UnityEngine;

// The game logo on the Main Menu floats up and down very slowly, like it is resting on the clouds.
// The distance is a small part of the logo's own height, so it looks the same at any logo size.
// Added by MenuButtonMotion when the Main Menu loads.
public class MenuLogoFloat : MonoBehaviour
{
    private const float Period = 4.5f;
    private const float HeightFraction = 0.025f;

    private Vector3 restPosition;
    private float amplitude;
    private float time;

    private void Start()
    {
        restPosition = transform.position;
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        amplitude = sprite != null ? sprite.bounds.size.y * HeightFraction : 0f;
    }

    private void Update()
    {
        if (amplitude <= 0f)
            return;

        time += Time.unscaledDeltaTime;
        transform.position = restPosition + Vector3.up * (Mathf.Sin(time / Period * Mathf.PI * 2f) * amplitude);
    }
}
