using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AnimalProceduralAnimator: Sistema orgânico de animação procedural para quadrúpedes e pequenos mamíferos (The Forest AAA).
/// Não requer esqueletos ósseos ou FBX externos: anima automaticamente membros, passadas, balanço de corpo,
/// respiração, estados de combate/fuga/colapso, flash de dano Zero-GC e LOD por distância.
/// </summary>
public class AnimalProceduralAnimator : MonoBehaviour
{
    public enum LocomotionStyle { QuadrupedStalker, QuadrupedUngulate, QuadrupedHeavy, Hopper }
    public enum AnimState { Idle, Move, Flee, TelegraphAttack, HitStagger, DeathCollapse }

    [Header("Animation Profile")]
    public LocomotionStyle locomotionStyle = LocomotionStyle.QuadrupedStalker;
    public AnimState currentState = AnimState.Idle;
    public float strideSpeedMultiplier = 3.5f;
    public float legSwingAngle = 26f;
    public float bodyBobAmount = 0.05f;
    public float bodyRollAmount = 3.5f;

    [Header("Anatomy Transforms (Auto-detected if null)")]
    public Transform bodyTransform;
    public Transform headTransform;
    public Transform tailTransform;
    public List<Transform> frontLegs = new List<Transform>();
    public List<Transform> backLegs = new List<Transform>();
    public List<Transform> ears = new List<Transform>();

    private Vector3 lastPosition;
    private float currentSpeed = 0f;
    private float strideCycle = 0f;
    private float idleTimer = 0f;
    private float earTwitchTimer = 0f;
    private float hitStaggerTimer = 0f;
    private float hitFlashTimer = 0f;
    private float deathCollapseProgress = 0f;

    private Quaternion initialBodyRot;
    private Vector3 initialBodyPos;
    private Vector3 initialBodyScale = Vector3.one;
    private Quaternion initialHeadRot;
    private Quaternion initialTailRot;
    private List<Quaternion> initialLegRotations = new List<Quaternion>();
    private List<Quaternion> initialEarRotations = new List<Quaternion>();
    private List<Transform> allLegs = new List<Transform>();

    // Cache Zero-GC para Hit Flash e LOD de Distancia
    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock hitFlashBlock;
    private Transform camTransform;
    private int frameParity;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        AutoDetectAnatomy();
        StoreInitialRotations();
        lastPosition = transform.position;
        strideCycle = Random.Range(0f, Mathf.PI * 2f);
        frameParity = Random.Range(0, 2);

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        hitFlashBlock = new MaterialPropertyBlock();
        if (Camera.main != null) camTransform = Camera.main.transform;
    }

    public void SetAnimState(AnimState state)
    {
        if (currentState == AnimState.DeathCollapse && state != AnimState.DeathCollapse) return;
        currentState = state;
        if (state == AnimState.HitStagger)
        {
            hitStaggerTimer = 0.28f;
        }
    }

    public void TriggerHitFlash()
    {
        hitFlashTimer = 0.16f;
        ApplyHitFlashBlock(1f);
        if (currentState != AnimState.DeathCollapse)
        {
            hitStaggerTimer = 0.28f;
            currentState = AnimState.HitStagger;
        }
    }

    private void ApplyHitFlashBlock(float intensity)
    {
        if (cachedRenderers == null || hitFlashBlock == null) return;

        if (intensity <= 0.001f)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null) cachedRenderers[i].SetPropertyBlock(null);
            }
            return;
        }

        Color tint = Color.Lerp(Color.white, new Color(1f, 0.28f, 0.22f, 1f), intensity);
        Color emit = new Color(0.85f, 0.14f, 0.10f, 1f) * (intensity * 1.4f);
        hitFlashBlock.SetColor(BaseColorId, tint);
        hitFlashBlock.SetColor(ColorId, tint);
        hitFlashBlock.SetColor(EmissionColorId, emit);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null) cachedRenderers[i].SetPropertyBlock(hitFlashBlock);
        }
    }

    private void AutoDetectAnatomy()
    {
        Transform[] children = GetComponentsInChildren<Transform>();
        frontLegs.Clear();
        backLegs.Clear();
        ears.Clear();
        allLegs.Clear();

        foreach (var t in children)
        {
            if (t == this.transform) continue;
            string n = t.name.ToLower();

            if (n.Contains("body") || n.Contains("corpo") || n.Contains("torso"))
            {
                if (bodyTransform == null) bodyTransform = t;
            }
            else if (n.Contains("head") || n.Contains("cabeca"))
            {
                if (headTransform == null) headTransform = t;
            }
            else if (n.Contains("tail") || n.Contains("rabo") || n.Contains("cauda"))
            {
                if (tailTransform == null) tailTransform = t;
            }
            else if (n.Contains("ear") || n.Contains("orelha"))
            {
                ears.Add(t);
            }
            else if (n.Contains("leg") || n.Contains("pata") || n.Contains("limb"))
            {
                allLegs.Add(t);
                if (t.localPosition.z >= 0f)
                    frontLegs.Add(t);
                else
                    backLegs.Add(t);
            }
        }

        if (bodyTransform == null && transform.childCount > 0)
        {
            bodyTransform = transform.GetChild(0);
        }
    }

    private void StoreInitialRotations()
    {
        if (bodyTransform != null)
        {
            initialBodyRot = bodyTransform.localRotation;
            initialBodyPos = bodyTransform.localPosition;
            initialBodyScale = bodyTransform.localScale.sqrMagnitude > 0.0001f ? bodyTransform.localScale : Vector3.one;
        }
        if (headTransform != null) initialHeadRot = headTransform.localRotation;
        if (tailTransform != null) initialTailRot = tailTransform.localRotation;

        initialLegRotations.Clear();
        foreach (var leg in allLegs)
        {
            initialLegRotations.Add(leg.localRotation);
        }

        initialEarRotations.Clear();
        foreach (var ear in ears)
        {
            initialEarRotations.Add(ear.localRotation);
        }
    }

    private void Update()
    {
        // Decay Zero-GC Hit Flash
        if (hitFlashTimer > 0f)
        {
            hitFlashTimer -= Time.deltaTime;
            ApplyHitFlashBlock(hitFlashTimer > 0f ? Mathf.Clamp01(hitFlashTimer / 0.16f) : 0f);
        }

        // Distance LOD baseado no GalicianOptimizationManager
        if (camTransform == null && Camera.main != null) camTransform = Camera.main.transform;
        float dt = Time.deltaTime;

        if (camTransform != null)
        {
            float cullDist = GalicianOptimizationManager.Instance != null
                ? GalicianOptimizationManager.Instance.wildlifeCullDist
                : 70f;
            float dx = transform.position.x - camTransform.position.x;
            float dz = transform.position.z - camTransform.position.z;
            float sqrDist = dx * dx + dz * dz;

            if (sqrDist > cullDist * cullDist)
            {
                lastPosition = transform.position;
                return;
            }

            if (sqrDist > 35f * 35f)
            {
                if (((Time.frameCount + frameParity) & 1) != 0)
                {
                    return;
                }
                dt *= 2f;
            }
        }

        // Calcular velocidade instantanea real no plano XZ
        Vector3 curPos = transform.position;
        Vector3 delta = curPos - lastPosition;
        delta.y = 0f;
        float dist = delta.magnitude;
        float targetSpeed = dist / Mathf.Max(0.0001f, dt);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Mathf.Clamp01(dt * 10f));
        lastPosition = curPos;

        if (currentState == AnimState.DeathCollapse)
        {
            AnimateDeathCollapse(dt);
            return;
        }

        if (hitStaggerTimer > 0f)
        {
            hitStaggerTimer -= dt;
            AnimateHitStagger(dt);
            if (hitStaggerTimer <= 0f && currentState == AnimState.HitStagger)
            {
                currentState = currentSpeed > 0.15f ? AnimState.Move : AnimState.Idle;
            }
            return;
        }

        if (currentState == AnimState.TelegraphAttack)
        {
            AnimateTelegraphAttack(dt);
            return;
        }

        bool isMoving = currentSpeed > 0.15f || currentState == AnimState.Move || currentState == AnimState.Flee;

        if (isMoving)
        {
            idleTimer = 0f;
            float fleeMult = (currentState == AnimState.Flee) ? 1.38f : 1.0f;
            float freq = (locomotionStyle == LocomotionStyle.Hopper) ? 6.0f : strideSpeedMultiplier;
            strideCycle += dt * Mathf.Max(currentSpeed, 1.2f) * freq * fleeMult;

            if (locomotionStyle == LocomotionStyle.Hopper)
            {
                AnimateHopper();
            }
            else
            {
                AnimateQuadruped(fleeMult);
            }
        }
        else
        {
            AnimateIdle(dt);
        }
    }

    private void AnimateTelegraphAttack(float dt)
    {
        float pulse = Mathf.Sin(Time.time * 14f);
        if (bodyTransform != null)
        {
            float rearPitch = (locomotionStyle == LocomotionStyle.QuadrupedHeavy) ? -14f : -7f;
            bodyTransform.localRotation = Quaternion.Slerp(
                bodyTransform.localRotation,
                initialBodyRot * Quaternion.Euler(rearPitch + pulse * 3f, 0f, pulse * 2.5f),
                dt * 10f);

            Vector3 lp = bodyTransform.localPosition;
            lp.y = initialBodyPos.y + 0.08f + Mathf.Abs(pulse) * 0.03f;
            bodyTransform.localPosition = lp;
        }

        if (headTransform != null)
        {
            headTransform.localRotation = Quaternion.Slerp(
                headTransform.localRotation,
                initialHeadRot * Quaternion.Euler(12f + pulse * 4f, 0f, 0f),
                dt * 12f);
        }
    }

    private void AnimateHitStagger(float dt)
    {
        float recoil = Mathf.Sin(Time.time * 42f) * 8f;
        if (bodyTransform != null)
        {
            bodyTransform.localRotation = Quaternion.Slerp(
                bodyTransform.localRotation,
                initialBodyRot * Quaternion.Euler(-11f, recoil * 0.5f, recoil),
                dt * 18f);
        }
        if (headTransform != null)
        {
            headTransform.localRotation = initialHeadRot * Quaternion.Euler(-16f, recoil, 0f);
        }
    }

    private void AnimateDeathCollapse(float dt)
    {
        deathCollapseProgress = Mathf.Clamp01(deathCollapseProgress + dt * 3.5f);
        if (bodyTransform != null)
        {
            bodyTransform.localRotation = Quaternion.Slerp(
                initialBodyRot,
                initialBodyRot * Quaternion.Euler(0f, 0f, 86f),
                deathCollapseProgress);

            Vector3 lp = bodyTransform.localPosition;
            lp.y = Mathf.Lerp(initialBodyPos.y, initialBodyPos.y * 0.35f, deathCollapseProgress);
            bodyTransform.localPosition = lp;
        }
    }

    private void AnimateQuadruped(float speedScale = 1f)
    {
        float sin = Mathf.Sin(strideCycle);
        float cos = Mathf.Cos(strideCycle);

        float swingAngle = legSwingAngle * Mathf.Clamp01(Mathf.Max(currentSpeed, 1.5f) / 3.0f) * speedScale;

        for (int i = 0; i < frontLegs.Count; i++)
        {
            float legSign = (i % 2 == 0) ? 1f : -1f;
            int idx = allLegs.IndexOf(frontLegs[i]);
            if (idx >= 0 && idx < initialLegRotations.Count)
            {
                frontLegs[i].localRotation = initialLegRotations[idx] *
                    Quaternion.Euler(sin * swingAngle * legSign, 0f, 0f);
            }
        }

        for (int i = 0; i < backLegs.Count; i++)
        {
            float legSign = (i % 2 == 0) ? -1f : 1f;
            int idx = allLegs.IndexOf(backLegs[i]);
            if (idx >= 0 && idx < initialLegRotations.Count)
            {
                backLegs[i].localRotation = initialLegRotations[idx] *
                    Quaternion.Euler(sin * swingAngle * legSign, 0f, 0f);
            }
        }

        if (bodyTransform != null)
        {
            float bob = Mathf.Abs(sin) * bodyBobAmount * speedScale;
            Vector3 localP = bodyTransform.localPosition;
            localP.y = initialBodyPos.y + bob;
            bodyTransform.localPosition = localP;

            float roll = cos * bodyRollAmount;
            bodyTransform.localRotation = initialBodyRot * Quaternion.Euler(sin * 1.5f, 0f, roll);
        }

        if (tailTransform != null)
        {
            tailTransform.localRotation = initialTailRot * Quaternion.Euler(18f + sin * 12f, cos * 15f, 0f);
        }

        if (headTransform != null)
        {
            headTransform.localRotation = initialHeadRot * Quaternion.Euler(-sin * 2.5f, 0f, 0f);
        }
    }

    private void AnimateHopper()
    {
        float hopPhase = Mathf.Sin(strideCycle);
        float hopHeight = Mathf.Max(0f, hopPhase) * 0.22f;

        if (bodyTransform != null)
        {
            Vector3 pos = bodyTransform.localPosition;
            pos.y = initialBodyPos.y + hopHeight;
            bodyTransform.localPosition = pos;

            float pitch = (hopPhase > 0f) ? -hopPhase * 18f : hopPhase * 10f;
            bodyTransform.localRotation = initialBodyRot * Quaternion.Euler(pitch, 0f, 0f);
        }

        for (int i = 0; i < allLegs.Count && i < initialLegRotations.Count; i++)
        {
            float legPitch = (hopPhase > 0.2f) ? 22f : -12f;
            allLegs[i].localRotation = initialLegRotations[i] * Quaternion.Euler(legPitch, 0f, 0f);
        }
    }

    private void AnimateIdle(float dt)
    {
        idleTimer += dt;
        float breathSin = Mathf.Sin(idleTimer * 2.2f);

        if (bodyTransform != null)
        {
            bodyTransform.localRotation = Quaternion.Slerp(bodyTransform.localRotation, initialBodyRot, dt * 4f);
            Vector3 scale = initialBodyScale * (1f + breathSin * 0.015f);
            bodyTransform.localScale = scale;
        }

        for (int i = 0; i < allLegs.Count && i < initialLegRotations.Count; i++)
        {
            allLegs[i].localRotation = Quaternion.Slerp(allLegs[i].localRotation, initialLegRotations[i], dt * 6f);
        }

        if (headTransform != null)
        {
            float headYaw = Mathf.Sin(idleTimer * 0.7f) * 12f;
            float headPitch = Mathf.Cos(idleTimer * 1.1f) * 6f;
            headTransform.localRotation = Quaternion.Slerp(headTransform.localRotation,
                initialHeadRot * Quaternion.Euler(headPitch, headYaw, 0f), dt * 3f);
        }

        if (tailTransform != null)
        {
            tailTransform.localRotation = initialTailRot * Quaternion.Euler(10f + breathSin * 4f, Mathf.Sin(idleTimer * 1.5f) * 8f, 0f);
        }

        earTwitchTimer += dt;
        if (earTwitchTimer > 2.5f)
        {
            float twitch = Mathf.Sin(Time.time * 30f) * 15f;
            for (int i = 0; i < ears.Count && i < initialEarRotations.Count; i++)
            {
                ears[i].localRotation = initialEarRotations[i] * Quaternion.Euler(twitch, 0f, 0f);
            }
            if (earTwitchTimer > 2.7f) earTwitchTimer = 0f;
        }
        else
        {
            for (int i = 0; i < ears.Count && i < initialEarRotations.Count; i++)
            {
                ears[i].localRotation = Quaternion.Slerp(ears[i].localRotation, initialEarRotations[i], dt * 8f);
            }
        }
    }
}
