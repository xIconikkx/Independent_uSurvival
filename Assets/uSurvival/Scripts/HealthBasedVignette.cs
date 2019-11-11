// Based on Unity's example script:
// https://forum.unity.com/threads/new-post-processing-stack-pre-release.435581/page-2#post-2845653
using UnityEngine;
using UnityEngine.PostProcessing;

[RequireComponent(typeof(PostProcessingBehaviour))]
public class HealthBasedVignette : MonoBehaviour
{
    public PostProcessingBehaviour behaviour; // assign in Inspector
    public float healthBasedSpeedMultiplier = 1;

    void Awake()
    {
        // create runtime profile so the project files aren't modified permanently
        behaviour.profile = Instantiate(behaviour.profile);
    }

    void SetVignetteSmoothness(float value)
    {
        VignetteModel.Settings vignette = behaviour.profile.vignette.settings;
        vignette.smoothness = value;
        behaviour.profile.vignette.settings = vignette;
    }

    void Update()
    {
        GameObject player = Player.localPlayer;
        if (!player) return;

        float healthPercent = player.GetComponent<Health>().Percent();
        float speed = 1 + (1 - healthPercent) * healthBasedSpeedMultiplier; // scale speed with health
        float wave = Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * speed));
        SetVignetteSmoothness((1 - healthPercent) * (0.5f + (wave / 2f)));
    }
}