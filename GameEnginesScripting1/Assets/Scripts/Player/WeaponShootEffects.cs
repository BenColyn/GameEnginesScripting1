using UnityEngine;

/// <summary>
/// 运行时枪口闪光与命中火花（不依赖预制体资源；URP/Built-in 下尽量选用可用 Shader）。
/// </summary>
public static class WeaponShootEffects
{
    static Material _trailMat;

    public static Material GetTrailMaterial()
    {
        if (_trailMat != null)
            return _trailMat;

        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null || s.name.Contains("InternalError"))
            s = Shader.Find("Particles/Standard Unlit");
        if (s == null || s.name.Contains("InternalError"))
            s = Shader.Find("Sprites/Default");

        _trailMat = new Material(s != null ? s : Shader.Find("Standard"))
        {
            color = new Color(1f, 0.85f, 0.35f, 0.85f)
        };
        return _trailMat;
    }

    public static void PlayMuzzleFlash(Vector3 position, Vector3 forward)
    {
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.forward;

        var go = new GameObject("MuzzleFlashFX");
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized));

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.65f, 0.2f);
        light.range = 2.5f;
        light.intensity = 2.8f;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.06f;
        main.startLifetime = 0.05f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.02f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetTrailMaterial();

        ps.Play();
        Object.Destroy(go, 0.25f);
    }

    public static void PlayImpactSpark(Vector3 position, Vector3 approximateNormal)
    {
        var go = new GameObject("BulletImpactFX");
        go.transform.position = position;
        if (approximateNormal.sqrMagnitude > 1e-8f)
            go.transform.rotation = Quaternion.LookRotation(approximateNormal.normalized);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.08f;
        main.startLifetime = 0.12f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
        main.maxParticles = 18;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.05f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(0.6f, 0.4f, 0.2f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetTrailMaterial();

        ps.Play();
        Object.Destroy(go, 0.35f);
    }
}
