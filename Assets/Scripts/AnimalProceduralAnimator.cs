using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AnimalProceduralAnimator: Sistema orgânico de animação procedural para quadrúpedes e pequenos mamíferos (The Forest AAA).
/// Não requer esqueletos ósseos ou FBX externos: anima automaticamente membros, passadas, balanço de corpo,
/// respiração, inclinação de terreno e ciclo de salto de coelhos.
/// </summary>
public class AnimalProceduralAnimator : MonoBehaviour
{
    public enum LocomotionStyle { QuadrupedStalker, QuadrupedUngulate, QuadrupedHeavy, Hopper }

    [Header("Animation Profile")]
    public LocomotionStyle locomotionStyle = LocomotionStyle.QuadrupedStalker;
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
    private Quaternion initialBodyRot;
    private Quaternion initialHeadRot;
    private List<Quaternion> initialLegRotations = new List<Quaternion>();
    private List<Transform> allLegs = new List<Transform>();

    private void Awake()
    {
        AutoDetectAnatomy();
        StoreInitialRotations();
        lastPosition = transform.position;
        strideCycle = Random.Range(0f, Mathf.PI * 2f);
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
                // Classificar em dianteira ou traseira com base no Z local
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
        if (bodyTransform != null) initialBodyRot = bodyTransform.localRotation;
        if (headTransform != null) initialHeadRot = headTransform.localRotation;

        initialLegRotations.Clear();
        foreach (var leg in allLegs)
        {
            initialLegRotations.Add(leg.localRotation);
        }
    }

    private void Update()
    {
        // 1. Calcular velocidade instantânea real no plano XZ
        Vector3 curPos = transform.position;
        Vector3 delta = curPos - lastPosition;
        delta.y = 0f;
        float dist = delta.magnitude;
        float targetSpeed = dist / Mathf.Max(0.0001f, Time.deltaTime);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 10f);
        lastPosition = curPos;

        bool isMoving = currentSpeed > 0.15f;

        if (isMoving)
        {
            idleTimer = 0f;
            float freq = (locomotionStyle == LocomotionStyle.Hopper) ? 6.0f : strideSpeedMultiplier;
            strideCycle += Time.deltaTime * Mathf.Max(currentSpeed, 1.2f) * freq;

            if (locomotionStyle == LocomotionStyle.Hopper)
            {
                AnimateHopper();
            }
            else
            {
                AnimateQuadruped();
            }
        }
        else
        {
            AnimateIdle();
        }
    }

    private void AnimateQuadruped()
    {
        float sin = Mathf.Sin(strideCycle);
        float cos = Mathf.Cos(strideCycle);

        // 1. Balanço das Patas (Passada Diagonal Cruzada Natural)
        float swingAngle = legSwingAngle * Mathf.Clamp01(currentSpeed / 3.0f);
        
        for (int i = 0; i < frontLegs.Count; i++)
        {
            float legSign = (i % 2 == 0) ? 1f : -1f;
            frontLegs[i].localRotation = initialLegRotations[allLegs.IndexOf(frontLegs[i])] * 
                Quaternion.Euler(sin * swingAngle * legSign, 0f, 0f);
        }

        for (int i = 0; i < backLegs.Count; i++)
        {
            // Traseiras em oposição à dianteira do mesmo lado
            float legSign = (i % 2 == 0) ? -1f : 1f;
            backLegs[i].localRotation = initialLegRotations[allLegs.IndexOf(backLegs[i])] * 
                Quaternion.Euler(sin * swingAngle * legSign, 0f, 0f);
        }

        // 2. Balanço do Corpo (Pitch e Roll sutis ao caminhar)
        if (bodyTransform != null)
        {
            float bob = Mathf.Abs(sin) * bodyBobAmount;
            Vector3 localP = bodyTransform.localPosition;
            localP.y = bob;
            bodyTransform.localPosition = localP;

            float roll = cos * bodyRollAmount;
            bodyTransform.localRotation = initialBodyRot * Quaternion.Euler(sin * 1.5f, 0f, roll);
        }

        // 3. Balanço da Cauda
        if (tailTransform != null)
        {
            tailTransform.localRotation = Quaternion.Euler(30f + sin * 12f, cos * 15f, 0f);
        }

        // 4. Cabeça equilibrada
        if (headTransform != null)
        {
            headTransform.localRotation = initialHeadRot * Quaternion.Euler(-sin * 2.5f, 0f, 0f);
        }
    }

    private void AnimateHopper()
    {
        // Salto em arco parabólico característico de lebres e coelhos
        float hopPhase = Mathf.Sin(strideCycle);
        float hopHeight = Mathf.Max(0f, hopPhase) * 0.22f;

        if (bodyTransform != null)
        {
            Vector3 pos = bodyTransform.localPosition;
            pos.y = hopHeight;
            bodyTransform.localPosition = pos;

            float pitch = (hopPhase > 0f) ? -hopPhase * 18f : hopPhase * 10f;
            bodyTransform.localRotation = initialBodyRot * Quaternion.Euler(pitch, 0f, 0f);
        }

        // Patas se esticam no ápice do pulo e retraem na aterrissagem
        for (int i = 0; i < allLegs.Count; i++)
        {
            float legPitch = (hopPhase > 0.2f) ? 22f : -12f;
            allLegs[i].localRotation = initialLegRotations[i] * Quaternion.Euler(legPitch, 0f, 0f);
        }
    }

    private void AnimateIdle()
    {
        idleTimer += Time.deltaTime;
        float breathSin = Mathf.Sin(idleTimer * 2.2f);

        // Respiração suave no corpo
        if (bodyTransform != null)
        {
            bodyTransform.localRotation = Quaternion.Slerp(bodyTransform.localRotation, initialBodyRot, Time.deltaTime * 4f);
            Vector3 scale = Vector3.one * (1f + breathSin * 0.015f);
            bodyTransform.localScale = scale;
        }

        // Retornar patas à postura ereta
        for (int i = 0; i < allLegs.Count; i++)
        {
            allLegs[i].localRotation = Quaternion.Slerp(allLegs[i].localRotation, initialLegRotations[i], Time.deltaTime * 6f);
        }

        // Movimento de farejar / olhar ao redor da cabeça
        if (headTransform != null)
        {
            float headYaw = Mathf.Sin(idleTimer * 0.7f) * 12f;
            float headPitch = Mathf.Cos(idleTimer * 1.1f) * 6f;
            headTransform.localRotation = Quaternion.Slerp(headTransform.localRotation, 
                initialHeadRot * Quaternion.Euler(headPitch, headYaw, 0f), Time.deltaTime * 3f);
        }

        // Cauda relaxada com leve tremor
        if (tailTransform != null)
        {
            tailTransform.localRotation = Quaternion.Euler(20f + breathSin * 4f, Mathf.Sin(idleTimer * 1.5f) * 8f, 0f);
        }

        // Orelhas tremem ocasionalmente
        earTwitchTimer += Time.deltaTime;
        if (earTwitchTimer > 2.5f)
        {
            float twitch = Mathf.Sin(Time.time * 30f) * 15f;
            foreach (var ear in ears)
            {
                ear.localRotation *= Quaternion.Euler(twitch, 0f, 0f);
            }
            if (earTwitchTimer > 2.7f) earTwitchTimer = 0f;
        }
    }
}
